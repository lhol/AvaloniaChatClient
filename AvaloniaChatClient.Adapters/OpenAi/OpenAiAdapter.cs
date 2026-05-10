using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaChatClient.Adapters.Models;

namespace AvaloniaChatClient.Adapters.OpenAi;

/// <summary>
/// Adapter for the OpenAI Chat Completions API.
/// Compatible with: OpenAI (cloud), Ollama, vLLM, LM Studio (OpenAI-compat mode).
/// Endpoint: POST /v1/chat/completions
/// Streaming: Server-Sent Events with stream=true; falls back to single JSON response.
/// </summary>
public sealed class OpenAiAdapter : ILlmAdapter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public string ProtocolName => "OpenAI";
    public bool SupportsFileAttachments => false;

    public OpenAiAdapter(HttpClient http)
    {
        _http = http;
    }

    // -------------------------------------------------------------------------
    // StreamAsync
    // -------------------------------------------------------------------------

    public async IAsyncEnumerable<LlmChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var httpRequest = BuildRequest(request, stream: true);
        using var httpResponse = await _http.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        httpResponse.EnsureSuccessStatusCode();

        var contentType = httpResponse.Content.Headers.ContentType?.MediaType;

        if (contentType == "text/event-stream")
        {
            await foreach (var chunk in ReadSseStreamAsync(httpResponse, cancellationToken))
                yield return chunk;
        }
        else
        {
            // Fallback: single JSON response wrapped as one chunk
            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var delta = ParseCompletionDelta(json);
            yield return new LlmChunk(delta, false, 0);
            yield return new LlmChunk(null, IsDone: true, 0, 0);
        }
    }

    // -------------------------------------------------------------------------
    // CompleteAsync
    // -------------------------------------------------------------------------

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = BuildRequest(request, stream: false);
        var sw = Stopwatch.StartNew();

        using var httpResponse = await _http.SendAsync(httpRequest, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        var ttftMs = sw.ElapsedMilliseconds;
        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        var totalMs = sw.ElapsedMilliseconds;

        var content = ParseCompletionDelta(json) ?? string.Empty;
        return new LlmResponse(content, ttftMs, totalMs);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private HttpRequestMessage BuildRequest(LlmRequest request, bool stream)
    {
        var messages = BuildMessageList(request);

        var body = new OpenAiChatRequest(
            request.ModelId,
            messages,
            stream,
            request.Temperature,
            request.MaxTokens);

        var json = JsonSerializer.Serialize(body, JsonOpts);
        var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{request.BaseUrl}/v1/chat/completions");
        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(request.Token))
            httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Token);

        return httpReq;
    }

    private static List<OpenAiMessage> BuildMessageList(LlmRequest request)
    {
        var list = new List<OpenAiMessage>(request.Messages.Count);
        foreach (var m in request.Messages)
            list.Add(new OpenAiMessage(m.Role, m.Content));
        return list;
    }

    private static async IAsyncEnumerable<LlmChunk> ReadSseStreamAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var sw = Stopwatch.StartNew();
        long? ttftMs = null;
        int? inputTokens = null;
        int? outputTokens = null;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data:")) continue;

            var data = line["data:".Length..].Trim();

            if (data == "[DONE]")
            {
                yield return new LlmChunk(null, IsDone: true, ttftMs, sw.ElapsedMilliseconds, inputTokens, outputTokens);
                yield break;
            }

            // Try to read usage tokens from frames that carry them (some providers send usage in the last frame)
            ParseUsage(data, ref inputTokens, ref outputTokens);

            var delta = ParseStreamDelta(data);
            if (delta is null) continue;

            ttftMs ??= sw.ElapsedMilliseconds;
            yield return new LlmChunk(delta, IsDone: false, ttftMs, null);
        }

        // Stream ended without [DONE]
        yield return new LlmChunk(null, IsDone: true, ttftMs, sw.ElapsedMilliseconds, inputTokens, outputTokens);
    }

    /// <summary>Extracts token usage from a streaming frame that carries it (e.g. OpenAI stream_options).</summary>
    internal static void ParseUsage(string json, ref int? inputTokens, ref int? outputTokens)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("usage", out var usage)) return;
            if (usage.ValueKind == JsonValueKind.Null) return;
            if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
                inputTokens = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct2) && ct2.ValueKind == JsonValueKind.Number)
                outputTokens = ct2.GetInt32();
        }
        catch { /* ignore malformed */ }
    }

    /// <summary>Extracts the text delta from a streaming SSE data line.</summary>
    internal static string? ParseStreamDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)) return null;
            if (choices.GetArrayLength() == 0) return null;

            var first = choices[0];
            if (!first.TryGetProperty("delta", out var delta)) return null;
            if (!delta.TryGetProperty("content", out var content)) return null;
            if (content.ValueKind == JsonValueKind.Null) return null;

            return content.GetString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Extracts the message content from a non-streaming completion response.</summary>
    internal static string? ParseCompletionDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)) return null;
            if (choices.GetArrayLength() == 0) return null;

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message)) return null;
            if (!message.TryGetProperty("content", out var content)) return null;

            return content.GetString();
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Internal request DTOs (serialized to snake_case JSON)
    // -------------------------------------------------------------------------

    private record OpenAiChatRequest(
        string Model,
        List<OpenAiMessage> Messages,
        bool Stream,
        double? Temperature,
        int? MaxTokens);

    private record OpenAiMessage(string Role, string Content);
}
