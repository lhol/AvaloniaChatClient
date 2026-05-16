using System.Collections.Generic;

namespace Avaimi.Adapters.Models;

public record LlmMessage(string Role, string Content);

public record LlmRequest(
    string BaseUrl,
    string ModelId,
    IReadOnlyList<LlmMessage> Messages,
    string? Token = null,
    IReadOnlyList<FileAttachment>? Files = null,
    double? Temperature = null,
    int? MaxTokens = null);

public record LlmChunk(string? Delta, bool IsDone, long? TtftMs = null, long? TotalMs = null, int? InputTokens = null, int? OutputTokens = null);

public record LlmResponse(string Content, long TtftMs, long TotalMs);

public record FileAttachment(string FileName, string MimeType, byte[] Data);
