using System;

namespace AvaloniaChatClient.Models;

public record ErrorLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string Context,
    string UserMessage,
    string FullDetails);
