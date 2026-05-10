namespace AvaloniaChatClient.Backend.Models;

public record ErrorLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string Context,
    string UserMessage,
    string FullDetails);

public record ErrorResponse(string UserMessage, Guid ErrorId);
