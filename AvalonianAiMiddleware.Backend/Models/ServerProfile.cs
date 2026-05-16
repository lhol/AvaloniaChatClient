namespace Avaimi.Backend.Models;

public enum LlmProtocol
{
    OpenAI,
    Anthropic,
    LmStudio,
    Custom
}

public record ServerProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public int Port { get; init; } = 11434;
    public string? Token { get; init; }
    public LlmProtocol Protocol { get; init; } = LlmProtocol.OpenAI;
    public string DefaultModel { get; init; } = "default";
}

public record CreateServerProfileRequest(
    string Name,
    string Url,
    int Port,
    string? Token,
    LlmProtocol Protocol,
    string? DefaultModel = null);

public record UpdateServerProfileRequest(
    string Name,
    string Url,
    int Port,
    string? Token,
    LlmProtocol Protocol,
    string? DefaultModel = null);

public record TestConnectionResponse(
    bool Success,
    long LatencyMs,
    string? Error);
