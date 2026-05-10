using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AvaloniaChatClient.Adapters.Models;
using AvaloniaChatClient.Backend.Models;
using AvaloniaChatClient.Backend.Services;

namespace AvaloniaChatClient.Backend.Endpoints;

public static class MessageEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly JsonSerializerOptions CamelOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sessions/{id:guid}/messages", async (
            Guid id,
            SendMessageRequest request,
            SessionService sessionSvc,
            ServerProfileService serverSvc,
            SkillService skillSvc,
            AdapterFactory adapterFactory,
            ErrorLogService errorLog,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var session = await sessionSvc.GetByIdAsync(id);
            if (session is null) { ctx.Response.StatusCode = 404; return; }

            var profile = await serverSvc.GetByIdAsync(session.ServerId);
            if (profile is null) { ctx.Response.StatusCode = 404; return; }

            var userMsg = new ChatMessage { Role = "user", Content = request.Content };
            await sessionSvc.AddMessageAsync(id, userMsg);

            var assistantMsg = new ChatMessage { Role = "assistant", Content = string.Empty };
            await sessionSvc.AddMessageAsync(id, assistantMsg);

            var llmMessages = new List<LlmMessage>();

            foreach (var skillId in session.SkillIds)
            {
                var md = await skillSvc.GetMarkdownAsync(skillId);
                if (md is not null)
                    llmMessages.Add(new LlmMessage("system", md));
            }

            foreach (var m in session.Messages.Where(m => !string.IsNullOrEmpty(m.Content)))
                llmMessages.Add(new LlmMessage(m.Role, m.Content));

            llmMessages.Add(new LlmMessage("user", request.Content));

            var baseUrl = $"{profile.Url}:{profile.Port}";
            var llmRequest = new LlmRequest(baseUrl, session.ModelId, llmMessages, profile.Token);

            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            var fullContent = new StringBuilder();
            long? ttftMs = null;
            int? inputTokens = null;
            int? outputTokens = null;
            var sw = Stopwatch.StartNew();

            try
            {
                var adapter = adapterFactory.Create(profile);

                await foreach (var chunk in adapter.StreamAsync(llmRequest, ct))
                {
                    if (chunk.IsDone)
                    {
                        sw.Stop();
                        inputTokens = chunk.InputTokens ?? CountWords(request.Content);
                        outputTokens = chunk.OutputTokens ?? CountWords(fullContent.ToString());
                        var done = new SseChunk(null, true, ttftMs ?? 0, sw.ElapsedMilliseconds,
                            inputTokens, outputTokens, profile.Name, session.ModelId);
                        await WriteSseAsync(ctx, done, ct);
                        await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
                        break;
                    }

                    if (chunk.Delta is null) continue;

                    ttftMs ??= chunk.TtftMs ?? sw.ElapsedMilliseconds;
                    fullContent.Append(chunk.Delta);

                    await WriteSseAsync(ctx, new SseChunk(chunk.Delta, false, ttftMs, null), ct);
                }

                await sessionSvc.UpdateLastAssistantMessageAsync(
                    id, fullContent.ToString(), ttftMs ?? 0, sw.ElapsedMilliseconds,
                    inputTokens, outputTokens, profile.Name, session.ModelId);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                var errorId = errorLog.Log($"SSE /sessions/{id}/messages", ex);
                // Send the error as a special SSE event so the frontend can display it
                var errorEvent = new SseErrorEvent(
                    "Fehler beim Abruf der KI-Antwort: " + ErrorLogService.ToUserMessage(ex),
                    errorId);
                await ctx.Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(errorEvent, CamelOpts)}\n\n", ct);
                await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
            }
        }).WithTags("Messages");

        return app;
    }

    private static async Task WriteSseAsync(HttpContext ctx, SseChunk chunk, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(chunk, JsonOpts);
        await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
}

