namespace Grove.Models;

public sealed class GroveConfig
{
    public List<RootConfig> Roots { get; set; } = new();
    public string DefaultCommand { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
    public List<CommandPreset> Presets { get; set; } = new();
    public Dictionary<string, WorktreeConfig> Worktrees { get; set; } = new();
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public int ConsoleFontSize { get; set; } = 13;
}
