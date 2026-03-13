using Grove.Models;
using Grove.Services;

namespace Grove.Tests;

public class CommandSyncServiceTests
{
    private static readonly string[] SiblingPaths =
        ["/repo/main", "/repo/feature-1", "/repo/feature-2"];

    private static GroveConfig CreateConfig(string? mainCmd = null, string? f1Cmd = null, string? f2Cmd = null)
    {
        var config = new GroveConfig();
        if (mainCmd is not null)
            config.Worktrees["/repo/main"] = new WorktreeConfig { Command = mainCmd };
        if (f1Cmd is not null)
            config.Worktrees["/repo/feature-1"] = new WorktreeConfig { Command = f1Cmd };
        if (f2Cmd is not null)
            config.Worktrees["/repo/feature-2"] = new WorktreeConfig { Command = f2Cmd };
        return config;
    }

    private static RootConfig CreateRoot(bool sync = false, string? defaultCmd = null) =>
        new() { Path = "/repo", SyncCommand = sync, DefaultCommand = defaultCmd };

    // ── LoadCommand ──────────────────────────────────────────────

    [Fact]
    public void LoadCommand_SyncOff_ReturnsOwnCommand()
    {
        var config = CreateConfig(mainCmd: "npm run dev", f1Cmd: "npm test");
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-1", root));
    }

    [Fact]
    public void LoadCommand_SyncOff_NoOwnCommand_ReturnsGlobalDefault()
    {
        var config = CreateConfig(mainCmd: "npm run dev");
        config.DefaultCommand = "echo hello";
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        Assert.Equal("echo hello", svc.LoadCommand("/repo/feature-1", root));
    }

    [Fact]
    public void LoadCommand_SyncOn_ReturnsRootDefaultCommand()
    {
        var config = CreateConfig(f1Cmd: "stale individual cmd");
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true, defaultCmd: "npm run dev");

        Assert.Equal("npm run dev", svc.LoadCommand("/repo/feature-1", root));
    }

    [Fact]
    public void LoadCommand_SyncOn_NoRootDefault_FallsBackToGlobal()
    {
        var config = new GroveConfig { DefaultCommand = "fallback" };
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true);

        Assert.Equal("fallback", svc.LoadCommand("/repo/main", root));
    }

    [Fact]
    public void LoadCommand_SyncOn_AllWorktreesGetSameCommand()
    {
        var config = CreateConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true, defaultCmd: "npm run dev");

        Assert.Equal("npm run dev", svc.LoadCommand("/repo/main", root));
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/feature-1", root));
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/feature-2", root));
    }

    [Fact]
    public void LoadCommand_NullRoot_ReturnsOwnCommand()
    {
        var config = CreateConfig(f1Cmd: "npm test");
        var svc = new CommandSyncService(config);

        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-1", null));
    }

    // ── SaveCommand ──────────────────────────────────────────────

    [Fact]
    public void SaveCommand_SyncOff_WritesPerWorktree()
    {
        var config = new GroveConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        svc.SaveCommand("/repo/feature-1", "npm test", root);

        Assert.Equal("npm test", config.Worktrees["/repo/feature-1"].Command);
    }

    [Fact]
    public void SaveCommand_SyncOff_DoesNotAffectSiblings()
    {
        var config = CreateConfig(mainCmd: "npm run dev", f1Cmd: "npm run dev");
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        svc.SaveCommand("/repo/feature-1", "changed", root);

        Assert.Equal("npm run dev", config.Worktrees["/repo/main"].Command);
        Assert.Equal("changed", config.Worktrees["/repo/feature-1"].Command);
    }

    [Fact]
    public void SaveCommand_SyncOn_WritesToRootDefault()
    {
        var config = CreateConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true, defaultCmd: "old");

        svc.SaveCommand("/repo/feature-1", "new-cmd", root);

        Assert.Equal("new-cmd", root.DefaultCommand);
    }

    [Fact]
    public void SaveCommand_SyncOn_ClearsPerWorktreeCommand()
    {
        var config = CreateConfig(f1Cmd: "stale");
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true, defaultCmd: "old");

        svc.SaveCommand("/repo/feature-1", "new-cmd", root);

        Assert.Null(config.Worktrees["/repo/feature-1"].Command);
    }

    // ── EnableSync ───────────────────────────────────────────────

    [Fact]
    public void EnableSync_SetsRootDefaultAndClearsWorktreeCommands()
    {
        var config = CreateConfig(mainCmd: "npm run dev", f1Cmd: "npm test", f2Cmd: "npm run build");
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        svc.EnableSync(root, "npm run dev", SiblingPaths);

        Assert.True(root.SyncCommand);
        Assert.Equal("npm run dev", root.DefaultCommand);
        Assert.Null(config.Worktrees["/repo/main"].Command);
        Assert.Null(config.Worktrees["/repo/feature-1"].Command);
        Assert.Null(config.Worktrees["/repo/feature-2"].Command);
    }

    [Fact]
    public void EnableSync_WorktreesWithNoConfigAreUnaffected()
    {
        var config = new GroveConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        svc.EnableSync(root, "npm run dev", SiblingPaths);

        Assert.True(root.SyncCommand);
        Assert.Equal("npm run dev", root.DefaultCommand);
        // No worktree configs should have been created
        Assert.Empty(config.Worktrees);
    }

    // ── DisableSync ──────────────────────────────────────────────

    [Fact]
    public void DisableSync_CopiesRootCommandToAllWorktrees()
    {
        var config = CreateConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true, defaultCmd: "npm run dev");

        svc.DisableSync(root, SiblingPaths);

        Assert.False(root.SyncCommand);
        Assert.Equal("npm run dev", config.Worktrees["/repo/main"].Command);
        Assert.Equal("npm run dev", config.Worktrees["/repo/feature-1"].Command);
        Assert.Equal("npm run dev", config.Worktrees["/repo/feature-2"].Command);
    }

    [Fact]
    public void DisableSync_NoRootDefault_UsesGlobalDefault()
    {
        var config = new GroveConfig { DefaultCommand = "fallback" };
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true);

        svc.DisableSync(root, SiblingPaths);

        Assert.All(SiblingPaths, path =>
            Assert.Equal("fallback", config.Worktrees[path].Command));
    }

    [Fact]
    public void DisableSync_PreservesEnvOverrides()
    {
        var config = CreateConfig(f1Cmd: "old");
        config.Worktrees["/repo/feature-1"].Env["PORT"] = "3001";
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: true, defaultCmd: "npm run dev");

        svc.DisableSync(root, SiblingPaths);

        Assert.Equal("npm run dev", config.Worktrees["/repo/feature-1"].Command);
        Assert.Equal("3001", config.Worktrees["/repo/feature-1"].Env["PORT"]);
    }

    // ── Full scenarios ───────────────────────────────────────────

    [Fact]
    public void Scenario_SyncedChangeFromAnyWorktreeReflectsEverywhere()
    {
        var config = CreateConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        // Enable sync with "npm run dev"
        svc.EnableSync(root, "npm run dev", SiblingPaths);

        // All worktrees should load "npm run dev"
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/main", root));
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/feature-1", root));
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/feature-2", root));

        // User changes command on feature-1 (sync is on → writes to root)
        svc.SaveCommand("/repo/feature-1", "npm test", root);

        // All worktrees should now load "npm test"
        Assert.Equal("npm test", svc.LoadCommand("/repo/main", root));
        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-1", root));
        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-2", root));

        // User changes command on feature-2
        svc.SaveCommand("/repo/feature-2", "npm run build", root);

        // All worktrees should now load "npm run build"
        Assert.Equal("npm run build", svc.LoadCommand("/repo/main", root));
        Assert.Equal("npm run build", svc.LoadCommand("/repo/feature-1", root));
        Assert.Equal("npm run build", svc.LoadCommand("/repo/feature-2", root));
    }

    [Fact]
    public void Scenario_DisableSyncThenChangesAreIndependent()
    {
        var config = CreateConfig();
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        // Enable sync, set command
        svc.EnableSync(root, "npm run dev", SiblingPaths);

        // Disable sync — copies "npm run dev" to all worktrees
        svc.DisableSync(root, SiblingPaths);

        // Change feature-1 independently
        svc.SaveCommand("/repo/feature-1", "npm test", root);

        // Others should be unaffected
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/main", root));
        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-1", root));
        Assert.Equal("npm run dev", svc.LoadCommand("/repo/feature-2", root));
    }

    [Fact]
    public void Scenario_ReenableSyncOverridesIndividualChanges()
    {
        var config = CreateConfig(mainCmd: "npm run dev", f1Cmd: "npm test", f2Cmd: "npm run build");
        var svc = new CommandSyncService(config);
        var root = CreateRoot(sync: false);

        // Enable sync from feature-1 with its current command
        svc.EnableSync(root, "npm test", SiblingPaths);

        // All should now load "npm test" (root default)
        Assert.Equal("npm test", svc.LoadCommand("/repo/main", root));
        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-1", root));
        Assert.Equal("npm test", svc.LoadCommand("/repo/feature-2", root));

        // Per-worktree commands should be cleared
        Assert.Null(config.Worktrees["/repo/main"].Command);
        Assert.Null(config.Worktrees["/repo/feature-1"].Command);
        Assert.Null(config.Worktrees["/repo/feature-2"].Command);
    }
}
