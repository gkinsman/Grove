using System.Diagnostics;
using System.Text;
using Grove.Services.Abstractions;

namespace Grove.Services;

public sealed class ShellService : IShellService
{
    public ProcessStartInfo CreateStartInfo(
        string command,
        string workingDirectory,
        Dictionary<string, string>? envOverrides = null)
    {
        var isWindows = OperatingSystem.IsWindows();

        // Sanitize: on Windows, prevent cmd.exe metacharacter injection by
        // wrapping the entire command in an outer pair of double-quotes.
        // cmd.exe /c "..." treats the quoted string as a single command.
        // On POSIX, escape embedded double-quotes for the -c argument.
        var escapedArgs = isWindows
            ? $"/c \"{command}\""
            : $"-c \"{command.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = escapedArgs,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (envOverrides is not null)
        {
            foreach (var (key, value) in envOverrides)
                psi.Environment[key] = value;
        }

        return psi;
    }
}
