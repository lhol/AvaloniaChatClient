using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avaimi.Adapters.Models;

namespace Avaimi.Adapters;

public interface ILlmAdapter
{
    string ProtocolName { get; }
    bool SupportsFileAttachments { get; }

    IAsyncEnumerable<LlmChunk> StreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);
}
