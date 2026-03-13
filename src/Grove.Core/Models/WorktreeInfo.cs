namespace Grove.Core.Models;

public sealed record WorktreeInfo(
    string Path,
    string HeadCommit,
    string BranchName,
    bool IsBare,
    string RepoRootPath
);
