using System.Diagnostics;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;

namespace Grove.Core.Services;

public sealed class GitService : IGitService
{
    public async Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(
        string repoPath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("git", "worktree list --porcelain")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return [];

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0) return [];

        return ParsePorcelainOutput(output, repoPath);
    }

    public async Task<IReadOnlyList<string>> DiscoverReposAsync(
        string scanPath, CancellationToken ct = default)
    {
        var repos = new List<string>();

        if (!Directory.Exists(scanPath)) return repos;

        await Task.Run(() =>
        {
            try
            {
                // Limit scan depth to 3 levels
                ScanDirectory(scanPath, repos, 0, maxDepth: 3, ct);
            }
            catch (OperationCanceledException)
            {
                // Propagate cancellation
                throw;
            }
            catch (Exception)
            {
                // Ignore scan errors (permission denied, etc.)
            }
        }, ct);

        return repos;
    }

    private static void ScanDirectory(
        string dir, List<string> repos, int depth, int maxDepth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (depth > maxDepth) return;

        // Check if this directory is a git repo
        if (Directory.Exists(Path.Combine(dir, ".git")) ||
            File.Exists(Path.Combine(dir, ".git")))
        {
            repos.Add(dir);
            return; // Don't recurse into git repos
        }

        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(dir))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(subDir);
                // Skip hidden directories and common non-repo dirs
                if (dirName.StartsWith('.') || dirName == "node_modules" || dirName == "bin" || dirName == "obj")
                    continue;
                ScanDirectory(subDir, repos, depth + 1, maxDepth, ct);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
    }

    public async Task<WorktreeInfo?> AddWorktreeAsync(
        string repoPath, string branchName, string? path = null, CancellationToken ct = default)
    {
        // Validate branch name: only allow legal git ref characters to prevent flag injection.
        // Legal chars: alphanumeric, '/', '.', '-', '_' (no leading '-', no '..' or '@{' sequences).
        if (string.IsNullOrWhiteSpace(branchName) ||
            branchName.StartsWith('-') ||
            branchName.Contains("..") ||
            branchName.Contains("@{") ||
            !branchName.All(c => char.IsLetterOrDigit(c) || c is '/' or '.' or '-' or '_'))
        {
            throw new ArgumentException($"Invalid branch name: {branchName}", nameof(branchName));
        }

        var targetPath = path ?? Path.Combine(
            Path.GetDirectoryName(repoPath)!,
            $"{Path.GetFileName(repoPath)}-{branchName.Replace("/", "-")}");

        // Use '--' separator to prevent branchName from being interpreted as a flag
        var psi = new ProcessStartInfo("git", $"worktree add \"{targetPath}\" -- \"{branchName}\"")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return null;

        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"git worktree add failed: {stderr}");

        var worktrees = await GetWorktreesAsync(repoPath, ct);
        return worktrees.FirstOrDefault(w => w.Path == targetPath);
    }

    public async Task<string?> GetUpstreamBranchAsync(
        string worktreePath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref @{upstream}")
        {
            WorkingDirectory = worktreePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return null;

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return proc.ExitCode == 0 ? output.Trim() : null;
    }

    private static List<WorktreeInfo> ParsePorcelainOutput(string output, string repoRoot)
    {
        var results = new List<WorktreeInfo>();
        var blocks = output.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            string? path = null, head = null, branch = null;
            var isBare = false;

            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("worktree ")) path = line[9..].Trim();
                else if (line.StartsWith("HEAD ")) head = line[5..].Trim();
                else if (line.StartsWith("branch ")) branch = line[7..].Trim().Replace("refs/heads/", "");
                else if (line.Trim() == "bare") isBare = true;
                else if (line.Trim() == "detached") branch = "(detached)";
            }

            if (path is not null && !isBare)
            {
                results.Add(new WorktreeInfo(
                    Path: path,
                    HeadCommit: head ?? string.Empty,
                    BranchName: branch ?? "(unknown)",
                    IsBare: false,
                    RepoRootPath: repoRoot));
            }
        }

        return results;
    }
}
