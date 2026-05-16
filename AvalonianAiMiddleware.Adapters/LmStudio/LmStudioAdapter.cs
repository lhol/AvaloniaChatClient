using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avaimi.Adapters.Models;
using Avaimi.Adapters.OpenAi;

namespace Avaimi.Adapters.LmStudio;

/// <summary>
/// LLM adapter for LM Studio servers.
///
/// LM Studio exposes a full OpenAI-compatible API at /v1/chat/completions,
/// so this adapter delegates all completions to <see cref="OpenAiAdapter"/>.
///
/// Use <see cref="LmStudioMetadataProvider"/> alongside this adapter to query
/// available models and hardware stats.
/// </summary>
public sealed class LmStudioAdapter : ILlmAdapter
{
    public string ProtocolName => "LM Studio";
    public bool SupportsFileAttachments => false;

    private readonly OpenAiAdapter _inner;

    public LmStudioAdapter(HttpClient http)
    {
        _inner = new OpenAiAdapter(http);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<LlmChunk> StreamAsync(LlmRequest request, CancellationToken ct = default)
        => _inner.StreamAsync(request, ct);

    /// <inheritdoc/>
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        => _inner.CompleteAsync(request, ct);
}
