using Grove.Models;

namespace Grove.Services.Abstractions;

public interface IGitService
{
    Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(string repoPath, CancellationToken ct = default);
    Task<IReadOnlyList<string>> DiscoverReposAsync(string scanPath, CancellationToken ct = default);
    Task<WorktreeInfo?> AddWorktreeAsync(string repoPath, string branchName, string? path = null, CancellationToken ct = default);
    Task<string?> GetUpstreamBranchAsync(string worktreePath, CancellationToken ct = default);
}
