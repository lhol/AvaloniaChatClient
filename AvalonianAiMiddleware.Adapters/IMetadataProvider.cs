using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avaimi.Adapters.Models;

namespace Avaimi.Adapters;

/// <summary>Provides server- and model-level metadata for a specific LLM server.</summary>
public interface IMetadataProvider
{
    /// <summary>Returns all model IDs currently loaded/available on the server.</summary>
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default);

    /// <summary>Returns hardware and runtime information. Returns an empty record on failure.</summary>
    Task<ServerMetadata> GetServerMetadataAsync(CancellationToken ct = default);
}
