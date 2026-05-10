using AvaloniaChatClient.Backend.Services;

namespace AvaloniaChatClient.Backend.Endpoints;

public static class ErrorEndpoints
{
    public static IEndpointRouteBuilder MapErrorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/errors").WithTags("Errors");

        group.MapGet("/", (ErrorLogService svc) =>
            Results.Ok(svc.GetAll()));

        group.MapDelete("/", (ErrorLogService svc) =>
        {
            svc.Clear();
            return Results.NoContent();
        });

        return app;
    }
}
