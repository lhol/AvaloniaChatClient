using AvaloniaChatClient.Backend.Models;
using AvaloniaChatClient.Backend.Services;

namespace AvaloniaChatClient.Backend.Endpoints;

public static class SkillEndpoints
{
    public static IEndpointRouteBuilder MapSkillEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/skills").WithTags("Skills");

        group.MapGet("/", async (SkillService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, SkillService svc) =>
            await svc.GetByIdAsync(id) is { } skill
                ? Results.Ok(skill)
                : Results.NotFound());

        group.MapDelete("/{id:guid}", async (Guid id, SkillService svc) =>
            await svc.DeleteAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}
