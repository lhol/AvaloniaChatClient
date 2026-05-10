using System;
using System.Collections.Generic;

namespace AvaloniaChatClient.Models;

public record ChatMessage(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset Timestamp,
    long? TtftMs,
    long? TotalMs,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? ServerName = null,   // G-09
    string? ModelName = null);   // G-09

public record ChatSession(
    Guid Id,
    string Title,
    string? Comment,             // G-03
    Guid ServerId,
    string ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<Guid> SkillIds,
    List<ChatMessage> Messages);

public record SessionSummary(
    Guid Id,
    string Title,
    string? Comment,             // G-03
    Guid ServerId,
    string ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

public record ExportSkillResponse(Guid SkillId, string Title);

public record SseChunk(
    string? Delta,
    bool IsDone,
    long? TtftMs,
    long? TotalMs,
    int? InputTokens = null,
    int? OutputTokens = null,
    string? ServerName = null,   // G-09
    string? ModelName = null);   // G-09

// Wird vom Backend gesendet wenn beim Streaming ein Fehler auftritt
public record SseErrorEvent(string ErrorMessage, Guid ErrorId);

