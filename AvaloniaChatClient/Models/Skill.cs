using System;

namespace AvaloniaChatClient.Models;

public record SkillSummary(Guid Id, string Title, DateTimeOffset CreatedAt);

public record SkillContent(Guid Id, string Title, string Markdown, DateTimeOffset CreatedAt);
