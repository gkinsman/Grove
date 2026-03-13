namespace Grove.Core.Models;

public sealed class CommandPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}
