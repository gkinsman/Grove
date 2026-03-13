using Grove.Models;

namespace Grove.Services.Abstractions;

public interface IConfigService
{
    GroveConfig Config { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    string ConfigDirectory { get; }
}
