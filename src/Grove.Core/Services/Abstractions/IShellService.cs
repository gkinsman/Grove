using System.Diagnostics;

namespace Grove.Core.Services.Abstractions;

public interface IShellService
{
    ProcessStartInfo CreateStartInfo(
        string command,
        string workingDirectory,
        Dictionary<string, string>? envOverrides = null);
}
