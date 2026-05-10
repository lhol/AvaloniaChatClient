using System.Text.Json;
using AvaloniaChatClient.Backend.Models;
using AvaloniaChatClient.Backend.Services;

namespace AvaloniaChatClient.Backend.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx, ErrorLogService errorLog)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected – not an error
        }
        catch (Exception ex)
        {
            var errorId = errorLog.Log("HTTP " + ctx.Request.Path, ex);
            _logger.LogError(ex, "Unhandled exception [{ErrorId}] at {Path}", errorId, ctx.Request.Path);

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";

                var body = JsonSerializer.Serialize(
                    new ErrorResponse("Interner Fehler – Details im Fehlerprotokoll.", errorId),
                    JsonOpts);
                await ctx.Response.WriteAsync(body);
            }
            // If SSE stream already started we can't change status code – stream was interrupted.
        }
    }
}
