using Grove.Models;

namespace Grove.Services;

/// <summary>
/// Handles loading and saving commands with root-level sync support.
/// 
/// When SyncCommand is ON:  all worktrees read/write from RootConfig.DefaultCommand.
///                           Per-worktree commands are cleared.
/// When SyncCommand is OFF: each worktree has its own command in WorktreeConfig.
///                           Turning off copies the root command to all worktrees.
/// </summary>
public sealed class CommandSyncService
{
    private readonly GroveConfig _config;

    public CommandSyncService(GroveConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Loads the command for a worktree.
    /// Sync ON  → RootConfig.DefaultCommand
    /// Sync OFF → WorktreeConfig.Command → GroveConfig.DefaultCommand
    /// </summary>
    public string LoadCommand(string worktreePath, RootConfig? rootConfig)
    {
        if (rootConfig?.SyncCommand == true && !string.IsNullOrEmpty(rootConfig.DefaultCommand))
            return rootConfig.DefaultCommand;

        if (_config.Worktrees.TryGetValue(worktreePath, out var wt) &&
            !string.IsNullOrEmpty(wt.Command))
            return wt.Command;

        return _config.DefaultCommand;
    }

    /// <summary>
    /// Saves the command. When synced, writes to the root. Otherwise writes per-worktree.
    /// </summary>
    public void SaveCommand(string worktreePath, string command, RootConfig? rootConfig)
    {
        if (rootConfig?.SyncCommand == true)
        {
            rootConfig.DefaultCommand = command;
            // Clear per-worktree command so it doesn't shadow the root
            if (_config.Worktrees.TryGetValue(worktreePath, out var wt))
                wt.Command = null;
        }
        else
        {
            EnsureWorktreeConfig(worktreePath).Command = command;
        }
    }

    /// <summary>
    /// Enables sync: stores the current command on the root, clears all per-worktree commands.
    /// </summary>
    public void EnableSync(RootConfig rootConfig, string currentCommand, IReadOnlyList<string> siblingPaths)
    {
        rootConfig.SyncCommand = true;
        rootConfig.DefaultCommand = currentCommand;

        foreach (var path in siblingPaths)
        {
            if (_config.Worktrees.TryGetValue(path, out var wt))
                wt.Command = null;
        }
    }

    /// <summary>
    /// Disables sync: copies the root command down to each worktree so they can diverge.
    /// </summary>
    public void DisableSync(RootConfig rootConfig, IReadOnlyList<string> siblingPaths)
    {
        var command = rootConfig.DefaultCommand ?? _config.DefaultCommand;
        rootConfig.SyncCommand = false;

        foreach (var path in siblingPaths)
        {
            EnsureWorktreeConfig(path).Command = command;
        }
    }

    private WorktreeConfig EnsureWorktreeConfig(string path)
    {
        if (!_config.Worktrees.TryGetValue(path, out var wt))
        {
            wt = new WorktreeConfig();
            _config.Worktrees[path] = wt;
        }
        return wt;
    }
}
