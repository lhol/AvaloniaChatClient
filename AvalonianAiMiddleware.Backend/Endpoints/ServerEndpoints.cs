using System.Text.Json;
using Avaimi.Backend.Models;
using Avaimi.Backend.Services;

namespace Avaimi.Backend.Endpoints;

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

        // G-08: list models available on a server
        group.MapGet("/{id:guid}/models", async (Guid id, ServerProfileService svc, HttpClient http) =>
        {
            var profile = await svc.GetByIdAsync(id);
            if (profile is null) return Results.NotFound();

            var baseUrl = $"{profile.Url}:{profile.Port}";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/models");
                if (!string.IsNullOrEmpty(profile.Token))
                    request.Headers.Authorization = new("Bearer", profile.Token);

                var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return Results.Ok(Array.Empty<string>());

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var models = new List<string>();
                if (doc.RootElement.TryGetProperty("data", out var data))
                    foreach (var item in data.EnumerateArray())
                        if (item.TryGetProperty("id", out var idProp))
                            models.Add(idProp.GetString() ?? string.Empty);

                return Results.Ok(models);
            }
            catch
            {
                return Results.Ok(Array.Empty<string>());
            }
        });

        return app;
    }
}
