namespace Avaimi.Adapters.Models;

/// <summary>Hardware and runtime metadata returned by an LLM server.</summary>
public record ServerMetadata(
    string? GpuName,
    long? VramTotalBytes,
    long? VramUsedBytes,
    long? RamTotalBytes,
    long? RamUsedBytes,
    string? ServerVersion)
{
    /// <summary>An empty instance used as a safe fallback.</summary>
    public static readonly ServerMetadata Empty = new(null, null, null, null, null, null);
}
