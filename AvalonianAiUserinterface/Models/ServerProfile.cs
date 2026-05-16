using System;

namespace Avaiu.Models;

public enum LlmProtocol { OpenAI, Anthropic, LmStudio, Custom }

public record ServerProfile(
    Guid Id,
    string Name,
    string Url,
    int Port,
    string? Token,
    LlmProtocol Protocol,
    string DefaultModel = "default");

public record TestConnectionResponse(bool Success, long LatencyMs, string? Error);
