using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IGitService _git;
    private readonly IConfigService _config;
    private readonly IProcessManager _processManager;
    private readonly IShellService _shell;

    // Sidebar data — roots with nested worktrees
    private readonly SourceCache<RootViewModel, string> _roots = new(r => r.Id);
    private readonly ReadOnlyObservableCollection<RootViewModel> _rootList;
    public ReadOnlyObservableCollection<RootViewModel> Roots => _rootList;

    // Selection
    private WorktreeViewModel? _selectedWorktree;
    public WorktreeViewModel? SelectedWorktree
    {
        get => _selectedWorktree;
        set => this.RaiseAndSetIfChanged(ref _selectedWorktree, value);
    }

    // Detail panel — derived from selection, disposes previous VM
    private readonly ObservableAsPropertyHelper<WorktreeDetailViewModel?> _detail;
    public WorktreeDetailViewModel? Detail => _detail.Value;

    // Settings panel
    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    public SettingsViewModel SettingsViewModel { get; }

    // Empty state
    private readonly ObservableAsPropertyHelper<bool> _hasRoots;
    public bool HasRoots => _hasRoots.Value;

    // Interactions — view registers handlers for these
    public Interaction<Unit, string?> PickFolder { get; } = new();

    // Commands
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> AddRootCommand { get; }
    public ReactiveCommand<RootViewModel, Unit> RemoveRootCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<WorktreeViewModel, Unit> SelectWorktreeCommand { get; }

    public MainWindowViewModel(
        IGitService git,
        IConfigService config,
        IProcessManager processManager,
        IShellService shell)
    {
        _git = git;
        _config = config;
        _processManager = processManager;
        _shell = shell;

        SettingsViewModel = new SettingsViewModel(config, PickFolder);

        // Bind roots to sorted observable collection
        _roots.Connect()
            .SortBy(r => r.Name)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _rootList)
            .Subscribe();

        // Detail VM derived from selection — dispose previous VM on switch
        _detail = this.WhenAnyValue(x => x.SelectedWorktree)
            .Select(wt => wt is null
                ? null
                : new WorktreeDetailViewModel(wt.Info, _processManager, _shell, _config))
            .Scan<WorktreeDetailViewModel?, WorktreeDetailViewModel?>(null, (prev, next) =>
            {
                prev?.Dispose();
                return next;
            })
            .ToProperty(this, x => x.Detail);

        // Empty state
        _hasRoots = _roots.CountChanged
            .Select(count => count > 0)
            .ToProperty(this, x => x.HasRoots);

        // Commands
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAllAsync);
        AddRootCommand = ReactiveCommand.CreateFromTask(AddRootAsync);
        RemoveRootCommand = ReactiveCommand.CreateFromTask<RootViewModel>(RemoveRootAsync);
        OpenSettingsCommand = ReactiveCommand.Create(() => { IsSettingsOpen = !IsSettingsOpen; });
        SelectWorktreeCommand = ReactiveCommand.Create<WorktreeViewModel>(wt => SelectedWorktree = wt);

        // Load on activation
        this.WhenActivated(disposables =>
        {
            // Single subscription for process status → sidebar binding (disposed on deactivation)
            _processManager.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(changeSet =>
                {
                    foreach (var change in changeSet)
                    {
                        var runner = change.Current;
                        var wt = _rootList
                            .SelectMany(r => r.Worktrees)
                            .FirstOrDefault(w => w.Info.Path == runner.WorktreePath);
                        wt?.BindToRunner(runner);
                    }
                })
                .DisposeWith(disposables);

            RefreshCommand.Execute().Subscribe().DisposeWith(disposables);
        });
    }

    private async Task RefreshAllAsync(CancellationToken ct)
    {
        await _config.LoadAsync(ct);

        var rootVms = new List<RootViewModel>();
        foreach (var rootConfig in _config.Config.Roots)
        {
            var rootVm = new RootViewModel(rootConfig, _git);
            rootVms.Add(rootVm);
        }

        _roots.Edit(cache =>
        {
            cache.Clear();
            foreach (var vm in rootVms)
                cache.AddOrUpdate(vm);
        });

        // Load worktrees for each root
        foreach (var rootVm in rootVms)
        {
            await rootVm.LoadWorktreesCommand.Execute();
        }
    }

    private async Task RemoveRootAsync(RootViewModel root, CancellationToken ct)
    {
        // Remove from config
        var configRoot = _config.Config.Roots.FirstOrDefault(r => r.Id == root.Id);
        if (configRoot != null)
        {
            _config.Config.Roots.Remove(configRoot);
            await _config.SaveAsync(ct);
        }

        // Remove from cache (updates UI)
        _roots.Remove(root);

        // Clear selection if it belonged to this root
        if (SelectedWorktree != null && root.Worktrees.Contains(SelectedWorktree))
            SelectedWorktree = null;
    }

    private async Task AddRootAsync(CancellationToken ct)
    {
        var folderPath = await PickFolder.Handle(Unit.Default);
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        await AddRootWithPath(folderPath);
    }

    public async Task AddRootWithPath(string folderPath)
    {
        var rootConfig = new RootConfig
        {
            Path = folderPath,
            Mode = RootMode.Repo
        };
        _config.Config.Roots.Add(rootConfig);
        await _config.SaveAsync();

        var rootVm = new RootViewModel(rootConfig, _git);
        _roots.AddOrUpdate(rootVm);
        await rootVm.LoadWorktreesCommand.Execute();
    }
}
