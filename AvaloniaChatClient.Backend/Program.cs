using System.Text.Json.Serialization;
using AvaloniaChatClient.Backend.Endpoints;
using AvaloniaChatClient.Backend.Middleware;
using AvaloniaChatClient.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<ServerProfileService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<SkillService>();
builder.Services.AddSingleton<AdapterFactory>();
builder.Services.AddSingleton<ErrorLogService>();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }))
    .WithTags("Health");

app.MapServerEndpoints();
app.MapSessionEndpoints();
app.MapMessageEndpoints();
app.MapSkillEndpoints();
app.MapErrorEndpoints();

app.Run();
