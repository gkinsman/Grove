using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly Interaction<Unit, string?> _pickFolder;

    // Roots
    private readonly SourceList<RootConfig> _roots = new();
    private readonly ReadOnlyObservableCollection<RootConfig> _rootList;
    public ReadOnlyObservableCollection<RootConfig> Roots => _rootList;

    // Presets
    private readonly SourceList<CommandPreset> _presets = new();
    private readonly ReadOnlyObservableCollection<CommandPreset> _presetList;
    public ReadOnlyObservableCollection<CommandPreset> PresetList => _presetList;

    // Global defaults
    private string _defaultCommand;
    public string DefaultCommand
    {
        get => _defaultCommand;
        set => this.RaiseAndSetIfChanged(ref _defaultCommand, value);
    }

    private bool _autoStart;
    public bool AutoStart
    {
        get => _autoStart;
        set => this.RaiseAndSetIfChanged(ref _autoStart, value);
    }

    // Appearance
    private AppTheme _theme;
    public AppTheme Theme
    {
        get => _theme;
        set => this.RaiseAndSetIfChanged(ref _theme, value);
    }

    private int _consoleFontSize;
    public int ConsoleFontSize
    {
        get => _consoleFontSize;
        set => this.RaiseAndSetIfChanged(ref _consoleFontSize, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> AddRootCommand { get; }
    public ReactiveCommand<RootConfig, Unit> RemoveRootCommand { get; }
    public ReactiveCommand<Unit, Unit> AddPresetCommand { get; }
    public ReactiveCommand<CommandPreset, Unit> RemovePresetCommand { get; }

    public SettingsViewModel(IConfigService config, Interaction<Unit, string?> pickFolder)
    {
        _config = config;
        _pickFolder = pickFolder;
        _defaultCommand = config.Config.DefaultCommand;
        _autoStart = config.Config.AutoStart;
        _theme = config.Config.Theme;
        _consoleFontSize = config.Config.ConsoleFontSize;

        _roots.AddRange(config.Config.Roots);
        _roots.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _rootList)
            .Subscribe();

        _presets.AddRange(config.Config.Presets);
        _presets.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _presetList)
            .Subscribe();

        // Auto-save on any property change
        this.WhenAnyValue(x => x.DefaultCommand, x => x.AutoStart, x => x.Theme, x => x.ConsoleFontSize)
            .Skip(1) // skip initial values
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => PersistSettings());

        AddRootCommand = ReactiveCommand.CreateFromTask(AddRootAsync);
        RemoveRootCommand = ReactiveCommand.Create<RootConfig>(RemoveRoot);
        AddPresetCommand = ReactiveCommand.Create(AddPreset);
        RemovePresetCommand = ReactiveCommand.Create<CommandPreset>(RemovePreset);
    }

    private void PersistSettings()
    {
        _config.Config.DefaultCommand = DefaultCommand;
        _config.Config.AutoStart = AutoStart;
        _config.Config.Theme = Theme;
        _config.Config.ConsoleFontSize = ConsoleFontSize;
        _config.Config.Roots = _rootList.ToList();
        _config.Config.Presets = _presetList.ToList();
        _ = _config.SaveAsync();
    }

    private async Task AddRootAsync(CancellationToken ct)
    {
        var folderPath = await _pickFolder.Handle(Unit.Default);
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        var newRoot = new RootConfig
        {
            Id = Guid.NewGuid().ToString(),
            Path = folderPath,
            Mode = RootMode.Repo
        };
        _roots.Add(newRoot);
        PersistSettings();
    }

    private void RemoveRoot(RootConfig root)
    {
        _roots.Remove(root);
        PersistSettings();
    }

    private void AddPreset()
    {
        var newPreset = new CommandPreset { Name = "New Preset", Command = string.Empty };
        _presets.Add(newPreset);
        PersistSettings();
    }

    private void RemovePreset(CommandPreset preset)
    {
        _presets.Remove(preset);
        PersistSettings();
    }
}
