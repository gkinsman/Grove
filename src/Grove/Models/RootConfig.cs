namespace Grove.Models;

public sealed class RootConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Path { get; set; } = string.Empty;
    public RootMode Mode { get; set; } = RootMode.Repo;
    public bool SyncCommand { get; set; }
    public string? DefaultCommand { get; set; }
}
