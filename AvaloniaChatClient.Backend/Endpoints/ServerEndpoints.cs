using AvaloniaChatClient.Backend.Models;
using AvaloniaChatClient.Backend.Services;

namespace AvaloniaChatClient.Backend.Endpoints;

public static class ServerEndpoints
{
    public static IEndpointRouteBuilder MapServerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/servers").WithTags("Servers");

        group.MapGet("/", async (ServerProfileService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, ServerProfileService svc) =>
            await svc.GetByIdAsync(id) is { } profile
                ? Results.Ok(profile)
                : Results.NotFound());

        group.MapPost("/", async (CreateServerProfileRequest request, ServerProfileService svc) =>
        {
            var profile = await svc.CreateAsync(request);
            return Results.Created($"/servers/{profile.Id}", profile);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateServerProfileRequest request, ServerProfileService svc) =>
            await svc.UpdateAsync(id, request) is { } updated
                ? Results.Ok(updated)
                : Results.NotFound());

        group.MapDelete("/{id:guid}", async (Guid id, ServerProfileService svc) =>
            await svc.DeleteAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapPost("/{id:guid}/test", async (Guid id, ServerProfileService svc, HttpClient http) =>
        {
            var profile = await svc.GetByIdAsync(id);
            if (profile is null) return Results.NotFound();

            var baseUrl = $"{profile.Url}:{profile.Port}";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/models");
                if (!string.IsNullOrEmpty(profile.Token))
                    request.Headers.Authorization = new("Bearer", profile.Token);

                var response = await http.SendAsync(request);
                sw.Stop();
                return Results.Ok(new TestConnectionResponse(response.IsSuccessStatusCode, sw.ElapsedMilliseconds, null));
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Results.Ok(new TestConnectionResponse(false, sw.ElapsedMilliseconds, ex.Message));
            }
        });

        return app;
    }
}
