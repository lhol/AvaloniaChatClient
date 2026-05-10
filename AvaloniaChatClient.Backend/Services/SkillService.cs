using AvaloniaChatClient.Backend.Models;

namespace AvaloniaChatClient.Backend.Services;

public class SkillService
{
    private readonly string _skillsDir;

    public SkillService(IConfiguration config)
    {
        var dataDir = config["DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaChatClient");
        _skillsDir = Path.Combine(dataDir, "skills");
        Directory.CreateDirectory(_skillsDir);
    }

    public Task<IReadOnlyList<SkillSummary>> GetAllAsync()
    {
        var skills = Directory.EnumerateFiles(_skillsDir, "*.md")
            .Select(f =>
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(f);
                var parts = nameWithoutExt.Split('_', 2);
                var id = Guid.TryParse(parts[0], out var g) ? g : Guid.Empty;
                var title = parts.Length > 1 ? parts[1].Replace('-', ' ') : nameWithoutExt;
                var created = File.GetCreationTimeUtc(f);
                return new SkillSummary(id, title, new DateTimeOffset(created, TimeSpan.Zero));
            })
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<SkillSummary>>(skills.AsReadOnly());
    }

    public async Task<SkillContent?> GetByIdAsync(Guid id)
    {
        var file = Directory.EnumerateFiles(_skillsDir, $"{id}_*.md").FirstOrDefault()
            ?? Directory.EnumerateFiles(_skillsDir, $"{id}.md").FirstOrDefault();
        if (file is null) return null;

        var markdown = await File.ReadAllTextAsync(file);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
        var parts = nameWithoutExt.Split('_', 2);
        var title = parts.Length > 1 ? parts[1].Replace('-', ' ') : nameWithoutExt;
        var created = File.GetCreationTimeUtc(file);
        return new SkillContent(id, title, markdown, new DateTimeOffset(created, TimeSpan.Zero));
    }

    public async Task<SkillSummary> CreateFromSessionAsync(Guid skillId, string title, string markdown)
    {
        var safeTitle = string.Concat(title.Split(Path.GetInvalidFileNameChars())).Replace(' ', '-');
        var fileName = $"{skillId}_{safeTitle}.md";
        await File.WriteAllTextAsync(Path.Combine(_skillsDir, fileName), markdown);
        return new SkillSummary(skillId, title, DateTimeOffset.UtcNow);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var file = Directory.EnumerateFiles(_skillsDir, $"{id}_*.md").FirstOrDefault()
            ?? Directory.EnumerateFiles(_skillsDir, $"{id}.md").FirstOrDefault();
        if (file is null) return Task.FromResult(false);
        File.Delete(file);
        return Task.FromResult(true);
    }

    public async Task<string?> GetMarkdownAsync(Guid id)
        => (await GetByIdAsync(id))?.Markdown;
}
