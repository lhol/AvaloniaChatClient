using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avaimi.Adapters.Models;

namespace Avaimi.Adapters.LmStudio;

/// <summary>
/// Fetches model and system metadata from a running LM Studio server.
///
/// Supported endpoints (LM Studio 0.2.x+):
///   GET /v1/models          – list loaded model IDs
///   GET /v1/system_info     – GPU/RAM stats (LM Studio 0.3.x+, optional)
///
/// TTFT is measured client-side in LmStudioAdapter; it is not part of the REST response.
/// </summary>
public sealed class LmStudioMetadataProvider : IMetadataProvider
{
    private readonly HttpClient _http;

    public LmStudioMetadataProvider(string baseUrl, string? token = null)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ModelsResponse>("v1/models", ct);
            return response?.Data?.Select(m => m.Id).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<ServerMetadata> GetServerMetadataAsync(CancellationToken ct = default)
    {
        try
        {
            var info = await _http.GetFromJsonAsync<SystemInfoResponse>("v1/system_info", ct);
            if (info is null) return ServerMetadata.Empty;
            return new ServerMetadata(
                GpuName: info.GpuName,
                VramTotalBytes: info.VramTotal,
                VramUsedBytes: info.VramUsed,
                RamTotalBytes: info.RamTotal,
                RamUsedBytes: info.RamUsed,
                ServerVersion: info.Version);
        }
        catch
        {
            // /v1/system_info is optional – graceful degradation
            return ServerMetadata.Empty;
        }
    }

    // ── JSON DTOs ─────────────────────────────────────────────────────────────

    private sealed record ModelsResponse([property: JsonPropertyName("data")] List<ModelEntry>? Data);

    private sealed record ModelEntry([property: JsonPropertyName("id")] string Id);

    private sealed record SystemInfoResponse(
        [property: JsonPropertyName("gpu_name")]   string?  GpuName,
        [property: JsonPropertyName("vram_total")] long?    VramTotal,
        [property: JsonPropertyName("vram_used")]  long?    VramUsed,
        [property: JsonPropertyName("ram_total")]  long?    RamTotal,
        [property: JsonPropertyName("ram_used")]   long?    RamUsed,
        [property: JsonPropertyName("version")]    string?  Version);
}
