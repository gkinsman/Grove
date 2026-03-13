using System.Reactive;
using System.Reactive.Linq;
using Grove.Models;
using Grove.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class AddWorktreeViewModel : ViewModelBase
{
    private readonly IGitService _git;
    private readonly string _repoPath;

    private string _branchName = string.Empty;
    public string BranchName
    {
        get => _branchName;
        set => this.RaiseAndSetIfChanged(ref _branchName, value);
    }

    private string _customPath = string.Empty;
    public string CustomPath
    {
        get => _customPath;
        set => this.RaiseAndSetIfChanged(ref _customPath, value);
    }

    private readonly ObservableAsPropertyHelper<bool> _canCreate;
    public bool CanCreate => _canCreate.Value;

    public ReactiveCommand<Unit, WorktreeInfo?> CreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public AddWorktreeViewModel(string repoPath, IGitService git)
    {
        _repoPath = repoPath;
        _git = git;

        _canCreate = this.WhenAnyValue(x => x.BranchName)
            .Select(b => !string.IsNullOrWhiteSpace(b))
            .ToProperty(this, x => x.CanCreate);

        var canExecute = this.WhenAnyValue(x => x.CanCreate);
        CreateCommand = ReactiveCommand.CreateFromTask(CreateWorktreeAsync, canExecute);
        CancelCommand = ReactiveCommand.Create(() => { });
    }

    private async Task<WorktreeInfo?> CreateWorktreeAsync(CancellationToken ct)
    {
        var path = string.IsNullOrWhiteSpace(CustomPath) ? null : CustomPath;
        return await _git.AddWorktreeAsync(_repoPath, BranchName, path, ct);
    }
}
