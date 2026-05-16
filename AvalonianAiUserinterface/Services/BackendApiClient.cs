using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avaiu.Models;

namespace Avaiu.Services;

public class BackendApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public BackendApiClient(string baseUrl = "http://localhost:5100")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    // ── Health ───────────────────────────────────────────────────────────────

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Server-Profile ───────────────────────────────────────────────────────

    public Task<List<ServerProfile>?> GetServersAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ServerProfile>>("/servers", JsonOpts, ct);

    public Task<ServerProfile?> GetServerAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<ServerProfile>($"/servers/{id}", JsonOpts, ct);

    public async Task<ServerProfile?> CreateServerAsync(
        string name, string url, int port, string? token, LlmProtocol protocol,
        string? defaultModel = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/servers",
            new { name, url, port, token, protocol, defaultModel }, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServerProfile>(JsonOpts, ct);
    }

    public async Task<ServerProfile?> UpdateServerAsync(
        Guid id, string name, string url, int port, string? token, LlmProtocol protocol,
        string? defaultModel = null, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/servers/{id}",
            new { name, url, port, token, protocol, defaultModel }, JsonOpts, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServerProfile>(JsonOpts, ct);
    }

    public async Task<bool> DeleteServerAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/servers/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<TestConnectionResponse?> TestServerAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/servers/{id}/test", null, ct);
        return await response.Content.ReadFromJsonAsync<TestConnectionResponse>(JsonOpts, ct);
    }

    // G-08: list models available on a server
    public async Task<List<string>> GetModelsAsync(Guid serverId, CancellationToken ct = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<string>>($"/servers/{serverId}/models", JsonOpts, ct);
            return result ?? [];
        }
        catch { return []; }
    }

    // ── Sessions ─────────────────────────────────────────────────────────────

    public Task<List<SessionSummary>?> GetSessionsAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<SessionSummary>>("/sessions", JsonOpts, ct);

    public Task<ChatSession?> GetSessionAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<ChatSession>($"/sessions/{id}", JsonOpts, ct);

    public async Task<ChatSession?> CreateSessionAsync(
        Guid serverId, string modelId, string? title = null, string? comment = null,
        string? topic = null, List<Guid>? skillIds = null,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/sessions",
            new { serverId, modelId, title, comment, topic, skillIds }, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSession>(JsonOpts, ct);
    }

    public async Task<bool> DeleteSessionAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/sessions/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    // G-04: update session title, comment, server, model, topic
    public async Task<ChatSession?> UpdateSessionMetaAsync(
        Guid id, string? title = null, string? comment = null, string? topic = null,
        Guid? serverId = null, string? modelId = null, CancellationToken ct = default)
    {
        var response = await _http.PatchAsJsonAsync($"/sessions/{id}/meta",
            new { title, comment, topic, serverId, modelId }, JsonOpts, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSession>(JsonOpts, ct);
    }

    public async Task<ExportSkillResponse?> ExportSessionAsSkillAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/sessions/{id}/export", null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExportSkillResponse>(JsonOpts, ct);
    }

    // ── Messages / SSE ───────────────────────────────────────────────────────

    public async IAsyncEnumerable<SseChunk> SendMessageAsync(
        Guid sessionId,
        string content,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/sessions/{sessionId}/messages");
        request.Content = JsonContent.Create(new { content }, options: JsonOpts);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (!line.StartsWith("data:")) continue;
            var data = line["data:".Length..].Trim();
            if (data == "[DONE]") yield break;

            // Check for backend error event first
            SseErrorEvent? errEvent = null;
            try { errEvent = JsonSerializer.Deserialize<SseErrorEvent>(data, JsonOpts); }
            catch { /* not an error event */ }

            if (errEvent?.ErrorMessage is not null)
            {
                // Surface as a special done-chunk so the caller can display the message
                yield return new SseChunk(null, true, null, null) with { Delta = $"⚠ {errEvent.ErrorMessage}" };
                yield break;
            }

            SseChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<SseChunk>(data, JsonOpts); }
            catch { /* malformed chunk – skip */ }

            if (chunk is not null) yield return chunk;
        }
    }

    // ── Skills ───────────────────────────────────────────────────────────────

    public Task<List<SkillSummary>?> GetSkillsAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<SkillSummary>>("/skills", JsonOpts, ct);

    public Task<SkillContent?> GetSkillAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<SkillContent>($"/skills/{id}", JsonOpts, ct);

    public async Task<bool> DeleteSkillAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/skills/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    // ── Backend Error Log ─────────────────────────────────────────────────────

    public Task<List<ErrorLogEntry>?> GetErrorsAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ErrorLogEntry>>("/errors", JsonOpts, ct);

    public async Task ClearErrorsAsync(CancellationToken ct = default)
    {
        await _http.DeleteAsync("/errors", ct);
    }
}
