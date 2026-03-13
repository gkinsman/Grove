using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class RootViewModel : ViewModelBase
{
    private readonly RootConfig _rootConfig;
    private readonly IGitService _git;

    public string Id => _rootConfig.Id;
    public RootConfig RootConfig => _rootConfig;
    public string Name => System.IO.Path.GetFileName(_rootConfig.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    public string Path => _rootConfig.Path;
    public RootMode Mode => _rootConfig.Mode;

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    private readonly SourceList<WorktreeViewModel> _worktrees = new();
    private readonly ReadOnlyObservableCollection<WorktreeViewModel> _worktreeList;
    public ReadOnlyObservableCollection<WorktreeViewModel> Worktrees => _worktreeList;

    public ReactiveCommand<Unit, Unit> LoadWorktreesCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleExpandCommand { get; }

    private readonly ObservableAsPropertyHelper<bool> _isLoading;
    public bool IsLoading => _isLoading.Value;

    public RootViewModel(RootConfig rootConfig, IGitService git)
    {
        _rootConfig = rootConfig;
        _git = git;

        _worktrees.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _worktreeList)
            .Subscribe();

        LoadWorktreesCommand = ReactiveCommand.CreateFromTask(LoadWorktreesAsync);

        _isLoading = LoadWorktreesCommand.IsExecuting
            .ToProperty(this, x => x.IsLoading);

        ToggleExpandCommand = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });
    }

    private async Task LoadWorktreesAsync(CancellationToken ct)
    {
        var repoPaths = _rootConfig.Mode == RootMode.Scan
            ? (IReadOnlyList<string>)await _git.DiscoverReposAsync(_rootConfig.Path, ct)
            : new[] { _rootConfig.Path };

        var allWorktrees = new List<WorktreeViewModel>();
        foreach (var repoPath in repoPaths)
        {
            var infos = await _git.GetWorktreesAsync(repoPath, ct);
            allWorktrees.AddRange(infos.Select(i => new WorktreeViewModel(i)));
        }

        _worktrees.Edit(list =>
        {
            list.Clear();
            list.AddRange(allWorktrees);
        });
    }
}
