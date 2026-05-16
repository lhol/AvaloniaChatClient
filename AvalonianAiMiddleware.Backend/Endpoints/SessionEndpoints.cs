using System.Text.Json;
using Avaimi.Backend.Models;
using Avaimi.Backend.Services;

namespace Avaimi.Backend.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions").WithTags("Sessions");

        group.MapGet("/", async (SessionService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, SessionService svc) =>
            await svc.GetByIdAsync(id) is { } session
                ? Results.Ok(session)
                : Results.NotFound());

        group.MapPost("/", async (CreateSessionRequest request, SessionService svc) =>
        {
            var session = await svc.CreateAsync(request);
            return Results.Created($"/sessions/{session.Id}", session);
        });

        group.MapDelete("/{id:guid}", async (Guid id, SessionService svc) =>
            await svc.DeleteAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        // G-04: update title, comment, server, model
        group.MapPatch("/{id:guid}/meta", async (Guid id, UpdateSessionMetaRequest request, SessionService svc) =>
            await svc.UpdateMetaAsync(id, request) is { } updated
                ? Results.Ok(updated)
                : Results.NotFound());

        group.MapPost("/{id:guid}/export", async (Guid id, SessionService svc, SkillService skillSvc) =>
        {
            var session = await svc.GetByIdAsync(id);
            if (session is null) return Results.NotFound();

            var markdown = BuildSkillMarkdown(session);
            var skillId = Guid.NewGuid();
            var title = session.Title;
            var skill = await skillSvc.CreateFromSessionAsync(skillId, title, markdown);
            return Results.Ok(new ExportSkillResponse(skill.Id, skill.Title));
        });

        return app;
    }

    private static string BuildSkillMarkdown(ChatSession session)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Erstellt:** {session.CreatedAt:yyyy-MM-dd}  ");
        sb.AppendLine($"**Modell:** {session.ModelId}");
        sb.AppendLine();
        sb.AppendLine("## Zusammenfassung");
        sb.AppendLine();
        foreach (var msg in session.Messages.Where(m => m.Role != "system"))
        {
            var role = msg.Role == "user" ? "**Benutzer**" : "**Assistent**";
            sb.AppendLine($"{role}: {msg.Content}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
