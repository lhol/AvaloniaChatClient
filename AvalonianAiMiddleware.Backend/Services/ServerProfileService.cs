using System.Text.Json;
using Avaimi.Backend.Models;

namespace Avaimi.Backend.Services;

public class ServerProfileService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private List<ServerProfile> _cache = [];
    private bool _loaded;

    public ServerProfileService(IConfiguration config)
    {
        var dataDir = config["DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Avaimi");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "servers.json");
    }

    public async Task<IReadOnlyList<ServerProfile>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _cache.AsReadOnly();
    }

    public async Task<ServerProfile?> GetByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _cache.FirstOrDefault(s => s.Id == id);
    }

    public async Task<ServerProfile> CreateAsync(CreateServerProfileRequest request)
    {
        await EnsureLoadedAsync();
        var profile = new ServerProfile
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Url = request.Url,
            Port = request.Port,
            Token = request.Token,
            Protocol = request.Protocol,
            DefaultModel = request.DefaultModel ?? "default"
        };
        _cache.Add(profile);
        await SaveAsync();
        return profile;
    }

    public async Task<ServerProfile?> UpdateAsync(Guid id, UpdateServerProfileRequest request)
    {
        await EnsureLoadedAsync();
        var index = _cache.FindIndex(s => s.Id == id);
        if (index < 0) return null;

        var updated = _cache[index] with
        {
            Name = request.Name,
            Url = request.Url,
            Port = request.Port,
            Token = request.Token,
            Protocol = request.Protocol,
            DefaultModel = request.DefaultModel ?? _cache[index].DefaultModel
        };
        _cache[index] = updated;
        await SaveAsync();
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await EnsureLoadedAsync();
        var removed = _cache.RemoveAll(s => s.Id == id);
        if (removed > 0) await SaveAsync();
        return removed > 0;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _cache = JsonSerializer.Deserialize<List<ServerProfile>>(json, _jsonOptions) ?? [];
        }
        _loaded = true;
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_cache, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
