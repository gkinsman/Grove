using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Grove.Models;
using Grove.Services.Abstractions;

namespace Grove.Services;

public sealed class ConfigService : IConfigService
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public string ConfigDirectory { get; } = GetConfigDirectory();

    private string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public GroveConfig Config { get; private set; } = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(ConfigPath))
        {
            Config = new GroveConfig();
            return;
        }

        try
        {
            await using var stream = File.OpenRead(ConfigPath);
            Config = await JsonSerializer.DeserializeAsync(
                stream, GroveJsonContext.Default.GroveConfig, ct) ?? new GroveConfig();
        }
        catch (JsonException)
        {
            // Corrupt config — start fresh
            Config = new GroveConfig();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            await using var stream = File.Create(ConfigPath);
            await JsonSerializer.SerializeAsync(
                stream, Config, GroveJsonContext.Default.GroveConfig, ct);

            // Apply restrictive file permissions — config may contain env secrets
            ApplyRestrictivePermissions(ConfigPath);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Restricts file access to the current user only.
    /// On Windows: sets ACL to owner-only. On POSIX: chmod 600.
    /// </summary>
    private static void ApplyRestrictivePermissions(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ApplyWindowsPermissions(filePath);
            }
            else
            {
                // POSIX: chmod 600 (owner read/write only)
                File.SetUnixFileMode(filePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (Exception)
        {
            // Best-effort — don't crash if permissions can't be set
            // (e.g., running in a container or restricted environment)
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void ApplyWindowsPermissions(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var security = fileInfo.GetAccessControl();

        // Remove all inherited rules
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        foreach (FileSystemAccessRule rule in rules)
        {
            security.RemoveAccessRule(rule);
        }

        // Grant full control to current user only
        var currentUser = WindowsIdentity.GetCurrent().User!;
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        fileInfo.SetAccessControl(security);
    }

    private static string GetConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grove");
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "grove");
    }
}
