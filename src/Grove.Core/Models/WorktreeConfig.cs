namespace Grove.Core.Models;

public sealed class WorktreeConfig
{
    public string? Command { get; set; }
    public Dictionary<string, string> Env { get; set; } = new();
}
