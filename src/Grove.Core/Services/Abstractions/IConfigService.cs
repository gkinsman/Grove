using Grove.Core.Models;

namespace Grove.Core.Services.Abstractions;

public interface IConfigService
{
    GroveConfig Config { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    string ConfigDirectory { get; }
}
