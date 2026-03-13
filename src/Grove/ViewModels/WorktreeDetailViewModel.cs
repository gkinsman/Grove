using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using Grove.Core.Models;
using Grove.Core.Services;
using Grove.Core.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class WorktreeDetailViewModel : ViewModelBase, IDisposable
{
    private readonly WorktreeInfo _info;
    private readonly IProcessManager _processManager;
    private readonly IShellService _shell;
    private readonly IConfigService _config;
    private readonly ConsoleBuffer _consoleBuffer = new();
    private readonly CompositeDisposable _disposables = new();

    // Header
    public string BranchName => _info.BranchName;
    public string FullPath => _info.Path;

    private string _upstreamBranch = string.Empty;
    public string UpstreamBranch
    {
        get => _upstreamBranch;
        set => this.RaiseAndSetIfChanged(ref _upstreamBranch, value);
    }

    // Status — derived from process runner
    private readonly ObservableAsPropertyHelper<ProcessStatus> _status;
    public ProcessStatus Status => _status.Value;

    private readonly ObservableAsPropertyHelper<string> _statusText;
    public string StatusText => _statusText.Value;

    private readonly ObservableAsPropertyHelper<bool> _isRunning;
    public bool IsRunning => _isRunning.Value;

    // Command bar
    private string _command = string.Empty;
    public string Command
    {
        get => _command;
        set => this.RaiseAndSetIfChanged(ref _command, value);
    }

    // Console
    private readonly ReadOnlyObservableCollection<ConsoleLine> _consoleLines;
    public ReadOnlyObservableCollection<ConsoleLine> ConsoleLines => _consoleLines;

    // Presets
    private readonly ReadOnlyObservableCollection<CommandPreset> _presets;
    public ReadOnlyObservableCollection<CommandPreset> Presets => _presets;

    // Elapsed time
    private readonly ObservableAsPropertyHelper<string?> _elapsed;
    public string? Elapsed => _elapsed.Value;

    // Commands
    public ReactiveCommand<Unit, Unit> RunCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> RestartCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearConsoleCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyConsoleCommand { get; }
    public ReactiveCommand<CommandPreset, Unit> LoadPresetCommand { get; }

    public WorktreeDetailViewModel(
        WorktreeInfo info,
        IProcessManager processManager,
        IShellService shell,
        IConfigService config)
    {
        _info = info;
        _processManager = processManager;
        _shell = shell;
        _config = config;

        var runner = _processManager.GetOrCreate(info.Path);

        // Load saved command for this worktree
        if (_config.Config.Worktrees.TryGetValue(info.Path, out var wtConfig))
            _command = wtConfig.Command ?? _config.Config.DefaultCommand;
        else
            _command = _config.Config.DefaultCommand;

        // Status from runner
        _status = runner.Status
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, x => x.Status);

        _statusText = this.WhenAnyValue(x => x.Status)
            .Select(s => s.ToString().ToLowerInvariant())
            .ToProperty(this, x => x.StatusText);

        _isRunning = this.WhenAnyValue(x => x.Status)
            .Select(s => s == ProcessStatus.Running)
            .ToProperty(this, x => x.IsRunning);

        // Console buffer — attach to runner output
        _consoleBuffer.Attach(runner.Output);
        _consoleBuffer.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _consoleLines)
            .Subscribe()
            .DisposeWith(_disposables);

        // Presets from config
        var presetsSource = new SourceList<CommandPreset>();
        presetsSource.DisposeWith(_disposables);
        presetsSource.AddRange(_config.Config.Presets);
        presetsSource.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _presets)
            .Subscribe()
            .DisposeWith(_disposables);

        // Elapsed time ticker
        _elapsed = this.WhenAnyValue(x => x.Status)
            .Select(s => s == ProcessStatus.Running
                ? Observable.Interval(TimeSpan.FromSeconds(30))
                    .Select(_ => FormatElapsed(runner.StartedAt))
                    .StartWith(FormatElapsed(runner.StartedAt))
                : Observable.Return<string?>(null))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, x => x.Elapsed);

        // Commands with CanExecute
        var canRun = this.WhenAnyValue(x => x.Status, x => x.Command,
            (s, c) => s != ProcessStatus.Running && s != ProcessStatus.Starting && !string.IsNullOrWhiteSpace(c));
        var canStop = this.WhenAnyValue(x => x.Status,
            s => s == ProcessStatus.Running || s == ProcessStatus.Starting);

        RunCommand = ReactiveCommand.Create(DoRun, canRun);
        StopCommand = ReactiveCommand.CreateFromTask(DoStopAsync, canStop);
        RestartCommand = ReactiveCommand.CreateFromTask(DoRestartAsync, canStop);
        ClearConsoleCommand = ReactiveCommand.Create(() => _consoleBuffer.Clear());
        CopyConsoleCommand = ReactiveCommand.CreateFromTask(DoCopyConsoleAsync);
        LoadPresetCommand = ReactiveCommand.Create<CommandPreset>(preset => Command = preset.Command);
    }

    private void DoRun()
    {
        try
        {
            var envOverrides = _config.Config.Worktrees
                .GetValueOrDefault(_info.Path)?.Env;
            var psi = _shell.CreateStartInfo(Command, _info.Path, envOverrides);
            var runner = _processManager.GetOrCreate(_info.Path);
            runner.Start(psi);
            SaveWorktreeCommand();
        }
        catch (Exception ex)
        {
            _consoleBuffer.AddLine($"[grove] Failed to start process: {ex.Message}");
        }
    }

    private async Task DoStopAsync(CancellationToken ct)
    {
        var runner = _processManager.GetOrCreate(_info.Path);
        await runner.StopAsync();
    }

    private async Task DoRestartAsync(CancellationToken ct)
    {
        var envOverrides = _config.Config.Worktrees
            .GetValueOrDefault(_info.Path)?.Env;
        var psi = _shell.CreateStartInfo(Command, _info.Path, envOverrides);
        var runner = _processManager.GetOrCreate(_info.Path);
        await runner.RestartAsync(psi);
    }

    private async Task DoCopyConsoleAsync(CancellationToken ct)
    {
        var text = string.Join(Environment.NewLine,
            _consoleLines.Select(l => l.RawText));

        // Clipboard access requires the TopLevel — handled in code-behind
        // Store text for the view to pick up
        ClipboardText = text;
        await Task.CompletedTask;
    }

    // Clipboard text for the view to copy
    public string? ClipboardText { get; private set; }

    private void SaveWorktreeCommand()
    {
        if (!_config.Config.Worktrees.ContainsKey(_info.Path))
            _config.Config.Worktrees[_info.Path] = new WorktreeConfig();
        _config.Config.Worktrees[_info.Path].Command = Command;
        _ = _config.SaveAsync();
    }

    private static string? FormatElapsed(DateTimeOffset? startedAt)
    {
        if (startedAt is null) return null;
        var elapsed = DateTimeOffset.Now - startedAt.Value;
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}m ago";
        return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m ago";
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _consoleBuffer.Dispose();
    }
}
