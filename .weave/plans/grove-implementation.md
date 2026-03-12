# Grove — Full Implementation Plan

## TL;DR
> **Summary**: Build Grove from scratch — a desktop git worktree manager with Avalonia UI (.NET 10), featuring a sidebar with collapsible repo groups, per-worktree command execution, live ANSI-colored console output, and system tray persistence.
> **Estimated Effort**: XL (6 phases, ~60 tasks)

## Context

### Original Request
Implement the complete Grove application as described in `GOAL.md` — a two-panel desktop app that discovers git worktrees across configured roots, lets users run/stop/restart commands per worktree, and streams live console output with ANSI color support. The app persists processes via system tray when the window is closed.

### Key Findings
- **Greenfield**: No code exists. Only `GOAL.md` and `image.png` (UI mockup) are present.
- **.NET 10.0.200** is installed. Avalonia MVVM template (`avalonia.mvvm`) is available.
- **CommunityToolkit.Mvvm 8.4.0** is the latest stable — supports .NET 8+ (compatible with .NET 10).
- **Avalonia TrayIcon** is built-in — defined in `App.axaml` with `NativeMenu`. No third-party package needed.
- **TreeView** with `TreeDataTemplate` supports hierarchical data with `ItemsSource` binding — perfect for roots → worktrees.
- **ANSI color rendering** is non-trivial in Avalonia. Best approach: parse ANSI escape codes into `InlineCollection` with `Run` elements styled with foreground colors. Use `SelectableTextBlock` (supports `Inlines`) inside an `ItemsRepeater` for virtualized line rendering.
- **UI mockup** (image.png) shows: dark theme, left sidebar (~280px) with root headers and worktree entries (dot + branch + path), right panel with header (branch, path, upstream, status badge), COMMAND section (text input + stop/restart buttons), preset chips row, and dark console area with colored output.

### Architecture Decisions
1. **Solution structure**: `Grove.sln` with two projects — `src/Grove` (Avalonia app) and `src/Grove.Core` (class library, no UI dependency).
2. **MVVM**: CommunityToolkit.Mvvm with source generators (`[ObservableProperty]`, `[RelayCommand]`).
3. **DI**: `Microsoft.Extensions.DependencyInjection` for service registration (lightweight, no Prism/DryIoc overhead).
4. **Git CLI**: Shell out via `System.Diagnostics.Process`, parse `git worktree list --porcelain` output.
5. **Config**: `System.Text.Json` with source generators for AOT-friendly serialization.
6. **Console**: Custom `AnsiParser` converts escape sequences to styled `Run` elements. `ItemsRepeater` virtualizes 10K-line ring buffer.
7. **Process management**: One `ManagedProcess` per worktree, wrapping `System.Diagnostics.Process` with async output streaming via `OutputDataReceived`/`ErrorDataReceived`.

## Objectives

### Core Objective
Deliver a fully functional v1 of Grove matching the design document and UI mockup.

### Deliverables
- [ ] Working Avalonia desktop app with two-panel layout
- [ ] Git worktree discovery (repo mode + scan mode)
- [ ] Per-worktree command execution with live console output
- [ ] ANSI color rendering in console panel
- [ ] Settings page (roots, presets, per-worktree overrides, appearance)
- [ ] System tray integration with process persistence
- [ ] Dark/light theme support

### Definition of Done
- [ ] `dotnet build src/Grove` succeeds with no errors
- [ ] `dotnet test` passes all unit tests (Core services)
- [ ] App launches, discovers worktrees from a configured root, runs a command, shows live output
- [ ] Closing window minimizes to tray; processes keep running
- [ ] Settings persist across restarts

### Guardrails (Must NOT)
- No libgit2 dependency — git CLI only
- No ReactiveUI — CommunityToolkit.Mvvm only
- No Electron/web — Avalonia native only
- No filesystem watcher (v2 feature)
- No CLI tool (v2 feature)

---

## TODOs

### Phase 1: Project Scaffolding

- [ ] 1.1 **Create solution and project structure** `[S]`
  **What**: Create `Grove.sln` with two projects using dotnet CLI. `src/Grove` is the Avalonia MVVM app (from `avalonia.mvvm` template). `src/Grove.Core` is a plain class library for non-UI logic (models, services, interfaces).
  **Files**:
  - `Grove.sln`
  - `src/Grove/Grove.csproj`
  - `src/Grove.Core/Grove.Core.csproj`
  **Commands**:
  ```
  dotnet new avalonia.mvvm -o src/Grove --name Grove --framework net10.0
  dotnet new classlib -o src/Grove.Core --name Grove.Core --framework net10.0
  dotnet new sln -n Grove
  dotnet sln add src/Grove src/Grove.Core
  dotnet add src/Grove reference src/Grove.Core
  ```
  **Acceptance**: `dotnet build` succeeds. Solution has two projects with correct references.

- [ ] 1.2 **Add NuGet packages** `[S]`
  **What**: Add required NuGet packages to both projects.
  **Files**:
  - `src/Grove/Grove.csproj` — add `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`
  - `src/Grove.Core/Grove.Core.csproj` — add `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection.Abstractions`
  **Packages**:
  - `CommunityToolkit.Mvvm` (8.4.0) — both projects
  - `Microsoft.Extensions.DependencyInjection` (10.0.x) — Grove only
  - `Microsoft.Extensions.DependencyInjection.Abstractions` — Grove.Core only
  **Acceptance**: `dotnet restore` succeeds. Packages listed in csproj files.

- [ ] 1.3 **Configure project settings** `[S]`
  **What**: Set `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, and enable CommunityToolkit.Mvvm source generators in both csproj files. Set `<RootNamespace>Grove.Core</RootNamespace>` for the Core project.
  **Files**: `src/Grove/Grove.csproj`, `src/Grove.Core/Grove.Core.csproj`
  **Acceptance**: Projects build with nullable enabled, source generators active.

- [ ] 1.4 **Set up folder structure** `[S]`
  **What**: Create the directory layout for both projects.
  **Directories**:
  ```
  src/Grove/
    Assets/          (icons, fonts)
    Views/           (AXAML views)
    ViewModels/      (VM classes)
    Controls/        (custom Avalonia controls)
    Converters/      (value converters)
    Styles/          (AXAML style resources)
  src/Grove.Core/
    Models/          (data models)
    Services/        (business logic services)
    Interfaces/      (service interfaces)
  ```
  **Acceptance**: Directories exist. No build errors.

- [ ] 1.5 **Clean up template boilerplate** `[S]`
  **What**: Remove the default `avalonia.mvvm` template's sample ViewModel and View content. Keep `App.axaml`, `App.axaml.cs`, `Program.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`. Strip sample greeting text. Set window title to "Grove" and minimum size to 900x600.
  **Files**: `src/Grove/MainWindow.axaml`, `src/Grove/ViewModels/MainWindowViewModel.cs`, `src/Grove/App.axaml.cs`
  **Acceptance**: App launches with an empty window titled "Grove".

- [ ] 1.6 **Set up DI container in App.axaml.cs** `[M]`
  **What**: Configure `Microsoft.Extensions.DependencyInjection` in `App.axaml.cs`. Register all services and ViewModels. Create a static `IServiceProvider` accessible via `App.Services`. Resolve `MainWindowViewModel` from DI when creating `MainWindow`.
  **Files**: `src/Grove/App.axaml.cs`
  **Key code**:
  ```csharp
  public partial class App : Application
  {
      public static IServiceProvider Services { get; private set; } = null!;
      
      public override void OnFrameworkInitializationCompleted()
      {
          var services = new ServiceCollection();
          ConfigureServices(services);
          Services = services.BuildServiceProvider();
          // ... set MainWindow with resolved VM
      }
  }
  ```
  **Acceptance**: App launches with DI. Services resolvable from `App.Services`.

- [ ] 1.7 **Add app icon assets** `[S]`
  **What**: Create a simple Grove tree icon (SVG or PNG) for the app window and tray. Place in `src/Grove/Assets/`. Add as `AvaloniaResource` in csproj. Can use a placeholder icon initially — a simple tree/leaf glyph.
  **Files**: `src/Grove/Assets/grove-icon.ico`, `src/Grove/Assets/grove-icon.png`
  **Acceptance**: Icon displays in window title bar.

---

### Phase 2: Core Services (No UI)

- [ ] 2.1 **Define data models** `[M]`
  **What**: Create all core data model classes in `Grove.Core/Models/`. These are plain C# records/classes serializable with System.Text.Json.
  **Files**: `src/Grove.Core/Models/`
  - `RootConfig.cs` — `{ string Id, string Path, RootMode Mode }` where `RootMode` is `enum { Repo, Scan }`
  - `WorktreeInfo.cs` — `{ string Path, string Branch, string HeadCommit, string? UpstreamBranch, string RootId, string RepoName }` (discovered from git, not persisted)
  - `WorktreeConfig.cs` — `{ string? Command, Dictionary<string, string> Env }` (persisted per-worktree settings)
  - `CommandPreset.cs` — `{ string Id, string Name, string Command }`
  - `GroveConfig.cs` — `{ List<RootConfig> Roots, string? DefaultCommand, bool AutoStart, List<CommandPreset> Presets, Dictionary<string, WorktreeConfig> Worktrees, AppearanceConfig Appearance }`
  - `AppearanceConfig.cs` — `{ ThemeMode Theme, int ConsoleFontSize }` where `ThemeMode` is `enum { Light, Dark, System }`
  - `ProcessStatus.cs` — `enum { Idle, Starting, Running, Error }`
  **Acceptance**: All models compile. JSON round-trip test passes.

- [ ] 2.2 **Implement ConfigService** `[M]`
  **What**: Service to load/save `GroveConfig` from JSON file. Handles platform-specific config paths (`%APPDATA%\grove\config.json` on Windows, `~/.config/grove/config.json` on Unix). Creates default config if file doesn't exist. Uses `System.Text.Json` with source generators for performance.
  **Files**:
  - `src/Grove.Core/Interfaces/IConfigService.cs`
  - `src/Grove.Core/Services/ConfigService.cs`
  - `src/Grove.Core/Services/GroveJsonContext.cs` (System.Text.Json source generator context)
  **Interface**:
  ```csharp
  public interface IConfigService
  {
      GroveConfig Config { get; }
      Task LoadAsync();
      Task SaveAsync();
      string ConfigDirectory { get; }
  }
  ```
  **Key details**:
  - Thread-safe save (use `SemaphoreSlim` to prevent concurrent writes)
  - Auto-create directory if missing
  - Debounced save (100ms) to avoid excessive disk writes when multiple settings change rapidly
  **Acceptance**: Unit test: save config, reload, verify equality.

- [ ] 2.3 **Implement GitService** `[L]`
  **What**: Service to discover git repos and parse worktree information by shelling out to `git` CLI.
  **Files**:
  - `src/Grove.Core/Interfaces/IGitService.cs`
  - `src/Grove.Core/Services/GitService.cs`
  **Interface**:
  ```csharp
  public interface IGitService
  {
      Task<List<WorktreeInfo>> GetWorktreesAsync(RootConfig root);
      Task<bool> IsGitRepoAsync(string path);
      Task<string?> GetUpstreamBranchAsync(string worktreePath);
      Task CreateWorktreeAsync(string repoPath, string branchName, string? baseBranch = null);
  }
  ```
  **Key implementation details**:
  - **Repo mode**: Run `git worktree list --porcelain` in root path. Parse output blocks separated by blank lines. Extract `worktree <path>`, `HEAD <sha>`, `branch refs/heads/<name>`.
  - **Scan mode**: Enumerate subdirectories of root. For each, check if `.git` exists (file or directory). If yes, run worktree list on it. Limit scan depth to 3 levels.
  - Run `git rev-parse --abbrev-ref @{upstream}` per worktree to get upstream branch (may fail for untracked branches — handle gracefully).
  - Derive `RepoName` from the repo's root directory name.
  - Use `Process.Start` with `RedirectStandardOutput`, `CreateNoWindow = true`.
  - Handle: bare repos (skip), detached HEAD (show commit SHA instead of branch), repos with no worktrees.
  **Acceptance**: Unit test with mock git output. Integration test against a real git repo with worktrees.

- [ ] 2.4 **Implement ProcessManager service** `[L]`
  **What**: Core service that spawns, monitors, and controls child processes. One process per worktree path.
  **Files**:
  - `src/Grove.Core/Interfaces/IProcessManager.cs`
  - `src/Grove.Core/Services/ProcessManager.cs`
  - `src/Grove.Core/Models/ManagedProcess.cs`
  **Interface**:
  ```csharp
  public interface IProcessManager
  {
      IReadOnlyDictionary<string, ManagedProcess> Processes { get; }
      ManagedProcess StartProcess(string worktreePath, string command, Dictionary<string, string>? envVars = null);
      Task StopProcessAsync(string worktreePath, TimeSpan? gracePeriod = null);
      Task RestartProcessAsync(string worktreePath, string command, Dictionary<string, string>? envVars = null);
      void StopAll();
      event Action<string, ProcessStatus>? StatusChanged;
      event Action<string, string>? OutputReceived; // worktreePath, line
  }
  ```
  **`ManagedProcess` class**:
  ```csharp
  public class ManagedProcess
  {
      public string WorktreePath { get; }
      public string Command { get; }
      public ProcessStatus Status { get; }
      public int? ExitCode { get; }
      public DateTime StartedAt { get; }
      public Process? SystemProcess { get; }
  }
  ```
  **Key implementation details**:
  - Platform shell: `cmd /c "<command>"` on Windows, `sh -c "<command>"` on Unix (use `RuntimeInformation.IsOSPlatform`).
  - Set `WorkingDirectory` to worktree path.
  - Merge env vars: copy `Environment.GetEnvironmentVariables()`, overlay per-worktree vars.
  - `RedirectStandardOutput = true`, `RedirectStandardError = true`, `CreateNoWindow = true`.
  - Subscribe to `OutputDataReceived` and `ErrorDataReceived` — fire `OutputReceived` event on each line.
  - On `Exited`: set status to `Idle` (exit 0) or `Error` (non-zero). Fire `StatusChanged`.
  - **Stop**: Send `Process.Kill(entireProcessTree: true)` on .NET 10. On Windows, can also try `Process.CloseMainWindow()` first. Use grace period (default 5s) before force kill.
  - **Prevent double-start**: If process already running for worktree, throw or return existing.
  - Thread safety: use `ConcurrentDictionary<string, ManagedProcess>`.
  **Acceptance**: Unit test: start process (`ping localhost` or `echo hello`), verify output received, stop, verify status change.

- [ ] 2.5 **Implement RingBuffer for console output** `[S]`
  **What**: A thread-safe ring buffer (circular buffer) that retains the last N lines (default 10,000). Used per worktree to store console output.
  **Files**: `src/Grove.Core/Services/RingBuffer.cs`
  **Interface**:
  ```csharp
  public class RingBuffer<T>
  {
      public RingBuffer(int capacity = 10_000);
      public void Add(T item);
      public void Clear();
      public int Count { get; }
      public IReadOnlyList<T> ToList(); // snapshot, oldest first
      public T this[int index] { get; }
  }
  ```
  **Implementation**: Array-backed with head/tail pointers. Lock-free reads if possible, or use `lock` for simplicity.
  **Acceptance**: Unit test: add 15K items to 10K buffer, verify count=10K, oldest items evicted.

- [ ] 2.6 **Implement ConsoleOutputManager** `[M]`
  **What**: Manages per-worktree console output buffers. Listens to `ProcessManager.OutputReceived`, routes lines to the correct `RingBuffer`, and raises events for the UI to consume.
  **Files**:
  - `src/Grove.Core/Interfaces/IConsoleOutputManager.cs`
  - `src/Grove.Core/Services/ConsoleOutputManager.cs`
  **Interface**:
  ```csharp
  public interface IConsoleOutputManager
  {
      RingBuffer<string> GetBuffer(string worktreePath);
      void Clear(string worktreePath);
      event Action<string, string>? LineAdded; // worktreePath, line
  }
  ```
  **Acceptance**: Unit test: simulate output, verify buffer contents and events.

- [ ] 2.7 **Implement ANSI parser** `[L]`
  **What**: Parse ANSI escape sequences (SGR codes) from console output lines and produce structured data that the UI can render as colored text. This is the most technically challenging piece.
  **Files**: `src/Grove.Core/Services/AnsiParser.cs`, `src/Grove.Core/Models/AnsiSpan.cs`
  **`AnsiSpan` model**:
  ```csharp
  public record AnsiSpan(string Text, AnsiColor? Foreground, AnsiColor? Background, bool Bold, bool Italic, bool Underline);
  public record AnsiColor(byte R, byte G, byte B);
  ```
  **Parser behavior**:
  - Input: raw string with ANSI escape codes (e.g., `\x1b[32mHello\x1b[0m`)
  - Output: `List<AnsiSpan>` — segments of text with associated styling
  - Support SGR codes: reset (0), bold (1), italic (3), underline (4), foreground colors (30-37, 90-97), background colors (40-47, 100-107), 256-color (38;5;N), true-color (38;2;R;G;B)
  - Strip unsupported escape sequences (cursor movement, etc.) — just remove them
  - Use a state machine: track current style, emit span when style changes or text ends
  **Standard ANSI color palette** (map codes 30-37 to RGB):
  - 30=Black(#1e1e1e), 31=Red(#f44747), 32=Green(#6a9955), 33=Yellow(#dcdcaa), 34=Blue(#569cd6), 35=Magenta(#c586c0), 36=Cyan(#4ec9b0), 37=White(#d4d4d4)
  - 90-97 = bright variants
  **Acceptance**: Unit test: parse `"\x1b[1;32mOK\x1b[0m done"` → `[AnsiSpan("OK", Green, Bold), AnsiSpan(" done", null, false)]`.

- [ ] 2.8 **Create test project** `[S]`
  **What**: Create `tests/Grove.Core.Tests` xUnit project with references to `Grove.Core`.
  **Files**:
  - `tests/Grove.Core.Tests/Grove.Core.Tests.csproj`
  - `tests/Grove.Core.Tests/Services/ConfigServiceTests.cs`
  - `tests/Grove.Core.Tests/Services/RingBufferTests.cs`
  - `tests/Grove.Core.Tests/Services/AnsiParserTests.cs`
  - `tests/Grove.Core.Tests/Services/GitServiceTests.cs`
  **Acceptance**: `dotnet test` runs and passes.

---

### Phase 3: MVVM ViewModels

- [ ] 3.1 **Create MainWindowViewModel** `[M]`
  **What**: Top-level ViewModel that orchestrates the sidebar and detail panel. Holds the selected worktree, manages navigation between worktree detail and settings views.
  **Files**: `src/Grove/ViewModels/MainWindowViewModel.cs`
  **Properties/Commands**:
  ```csharp
  [ObservableProperty] private ObservableCollection<RootViewModel> _roots;
  [ObservableProperty] private WorktreeViewModel? _selectedWorktree;
  [ObservableProperty] private object? _currentDetailView; // WorktreeDetailVM or SettingsVM
  [ObservableProperty] private bool _isSettingsOpen;
  
  [RelayCommand] private async Task RefreshAsync();
  [RelayCommand] private void OpenSettings();
  [RelayCommand] private void CloseSettings();
  [RelayCommand] private async Task AddRootAsync(); // opens folder picker
  ```
  **Behavior**:
  - On construction/refresh: call `IGitService.GetWorktreesAsync` for each root in config, build `RootViewModel` tree.
  - When `SelectedWorktree` changes, update `CurrentDetailView` to a `WorktreeDetailViewModel` for that worktree.
  - Expose aggregate process status for tray icon (computed from all `ManagedProcess` statuses).
  **Acceptance**: ViewModel constructs, refresh populates roots collection.

- [ ] 3.2 **Create RootViewModel** `[S]`
  **What**: Represents a root in the sidebar. Contains nested worktree items.
  **Files**: `src/Grove/ViewModels/RootViewModel.cs`
  **Properties**:
  ```csharp
  public string Name { get; }           // derived from folder name
  public string Path { get; }
  public RootMode Mode { get; }
  public ObservableCollection<WorktreeViewModel> Worktrees { get; }
  [ObservableProperty] private bool _isExpanded = true;
  ```
  **Acceptance**: Displays root name with collapsible worktree children.

- [ ] 3.3 **Create WorktreeViewModel** `[M]`
  **What**: Represents a single worktree entry in the sidebar. Tracks process status for the status indicator dot.
  **Files**: `src/Grove/ViewModels/WorktreeViewModel.cs`
  **Properties**:
  ```csharp
  public WorktreeInfo Info { get; }
  public string BranchName => Info.Branch;
  public string ShortPath { get; }      // abbreviated path
  [ObservableProperty] private ProcessStatus _status = ProcessStatus.Idle;
  ```
  **Behavior**:
  - Subscribe to `IProcessManager.StatusChanged` for this worktree's path.
  - Update `Status` property on status changes (must dispatch to UI thread).
  **Acceptance**: Status dot updates when process starts/stops.

- [ ] 3.4 **Create WorktreeDetailViewModel** `[L]`
  **What**: The main detail panel ViewModel. Shows header info, command bar, presets, and console output for the selected worktree.
  **Files**: `src/Grove/ViewModels/WorktreeDetailViewModel.cs`
  **Properties/Commands**:
  ```csharp
  // Header
  public string BranchName { get; }
  public string FullPath { get; }
  public string? UpstreamBranch { get; }
  [ObservableProperty] private ProcessStatus _status;
  [ObservableProperty] private DateTime? _startedAt;
  
  // Command bar
  [ObservableProperty] private string _command = "";
  public ObservableCollection<CommandPreset> Presets { get; }
  
  // Console
  public ObservableCollection<ConsoleLine> ConsoleLines { get; }
  
  // Commands
  [RelayCommand] private async Task RunAsync();
  [RelayCommand] private async Task StopAsync();
  [RelayCommand] private async Task RestartAsync();
  [RelayCommand] private void ClearConsole();
  [RelayCommand] private void LoadPreset(CommandPreset preset);
  [RelayCommand] private void CopyConsoleOutput();
  
  // Computed
  public bool CanRun => Status == ProcessStatus.Idle || Status == ProcessStatus.Error;
  public bool CanStop => Status == ProcessStatus.Running || Status == ProcessStatus.Starting;
  public string StatusText => Status.ToString().ToLower();
  public string ElapsedTime { get; } // "started Xm ago" — updated via timer
  ```
  **`ConsoleLine` model** (UI-specific, in Grove project):
  ```csharp
  public record ConsoleLine(List<AnsiSpan> Spans, DateTime Timestamp);
  ```
  **Behavior**:
  - On construction: load existing buffer from `IConsoleOutputManager`, parse each line through `AnsiParser`.
  - Subscribe to `IConsoleOutputManager.LineAdded` — parse and append to `ConsoleLines` (dispatch to UI thread).
  - Load command from config (per-worktree override or global default).
  - Save command to config on change (debounced).
  - Elapsed time: use a `DispatcherTimer` (1-minute interval) to update "started Xm ago" text.
  **Acceptance**: Run command → console lines appear. Stop → status updates. Presets load into command bar.

- [ ] 3.5 **Create SettingsViewModel** `[M]`
  **What**: ViewModel for the settings page. Manages roots, presets, per-worktree overrides, and appearance.
  **Files**: `src/Grove/ViewModels/SettingsViewModel.cs`
  **Properties/Commands**:
  ```csharp
  // Global defaults
  [ObservableProperty] private string _defaultCommand;
  [ObservableProperty] private bool _autoStart;
  
  // Roots
  public ObservableCollection<RootConfigViewModel> Roots { get; }
  [RelayCommand] private async Task AddRootAsync();
  [RelayCommand] private void RemoveRoot(RootConfigViewModel root);
  
  // Presets
  public ObservableCollection<PresetViewModel> Presets { get; }
  [RelayCommand] private void AddPreset();
  [RelayCommand] private void RemovePreset(PresetViewModel preset);
  
  // Per-worktree overrides
  public ObservableCollection<WorktreeOverrideViewModel> WorktreeOverrides { get; }
  
  // Appearance
  [ObservableProperty] private ThemeMode _theme;
  [ObservableProperty] private int _consoleFontSize;
  ```
  **Behavior**:
  - All changes auto-save to config (debounced via `ConfigService`).
  - Adding a root opens a folder picker dialog (`IStorageProvider.OpenFolderPickerAsync`).
  - Root mode toggle: repo vs scan.
  **Acceptance**: Add/remove roots persists. Theme change applies immediately.

- [ ] 3.6 **Create supporting sub-ViewModels** `[S]`
  **What**: Small ViewModels for settings sub-items.
  **Files**:
  - `src/Grove/ViewModels/RootConfigViewModel.cs` — wraps `RootConfig` with editable properties
  - `src/Grove/ViewModels/PresetViewModel.cs` — wraps `CommandPreset` with editable name/command
  - `src/Grove/ViewModels/WorktreeOverrideViewModel.cs` — wraps per-worktree config (path, command, env vars table)
  - `src/Grove/ViewModels/EnvVarViewModel.cs` — single key-value pair for env var editing
  **Acceptance**: All compile and bind correctly in settings view.

- [ ] 3.7 **Create ViewLocator / DataTemplate mappings** `[S]`
  **What**: Set up Avalonia `DataTemplate` mappings so that when `CurrentDetailView` is a `WorktreeDetailViewModel`, the `WorktreeDetailView` UserControl is shown, and when it's a `SettingsViewModel`, the `SettingsView` is shown. Use Avalonia's `ViewLocator` pattern or explicit `DataTemplate` declarations in `App.axaml`.
  **Files**: `src/Grove/App.axaml` (DataTemplates section), or `src/Grove/ViewLocator.cs`
  **Acceptance**: Switching `CurrentDetailView` type swaps the displayed view.

---

### Phase 4: Views & Styling

- [ ] 4.1 **Define color palette and theme resources** `[M]`
  **What**: Create AXAML resource dictionaries defining the Grove color palette for dark and light themes. Based on the mockup: dark charcoal background (#1e1e2e), sidebar slightly lighter (#252535), green accents (#4ec9b0), muted text (#888), console background (#0d0d14).
  **Files**:
  - `src/Grove/Styles/Colors.axaml` — color resource dictionary (brushes, colors)
  - `src/Grove/Styles/DarkTheme.axaml` — dark theme overrides
  - `src/Grove/Styles/LightTheme.axaml` — light theme overrides
  - `src/Grove/Styles/Base.axaml` — shared styles (fonts, spacing, common control styles)
  **Color tokens** (dark theme, from mockup):
  ```
  GroveBg:          #1a1a2e
  GroveSidebarBg:   #16161e
  GroveSurfaceBg:   #1e1e2e
  GroveConsoleBg:   #0d0d14
  GroveAccent:      #4ec9b0 (teal green)
  GroveText:        #d4d4d4
  GroveMuted:       #666680
  GroveError:       #f44747
  GroveWarning:     #dcdcaa
  GroveBorder:      #2d2d3f
  GroveRunning:     #4ec9b0 (green badge)
  GroveIdle:        #666680 (grey)
  GroveStarting:    #dcdcaa (amber)
  ```
  **Acceptance**: Resources load without errors. Theme switching works.

- [ ] 4.2 **Implement MainWindow.axaml layout** `[M]`
  **What**: Two-panel layout matching the mockup. Left sidebar (fixed ~280px or resizable via `GridSplitter`), right detail panel (fills remaining space). Use a `Grid` with two columns.
  **Files**: `src/Grove/MainWindow.axaml`, `src/Grove/MainWindow.axaml.cs`
  **Layout structure**:
  ```xml
  <Grid ColumnDefinitions="280,*">
    <!-- Sidebar -->
    <Border Grid.Column="0" Background="{DynamicResource GroveSidebarBg}">
      <views:SidebarView DataContext="{Binding}" />
    </Border>
    <!-- Detail panel -->
    <ContentControl Grid.Column="1" Content="{Binding CurrentDetailView}" />
  </Grid>
  ```
  **Window properties**: `Title="Grove"`, `MinWidth="900"`, `MinHeight="600"`, `Background="{DynamicResource GroveBg}"`, window icon from assets.
  **Acceptance**: Two-panel layout renders. Sidebar on left, detail on right.

- [ ] 4.3 **Implement SidebarView** `[L]`
  **What**: The left panel showing the Grove logo/title at top, then a scrollable list of roots with nested worktrees, and "add" buttons at the bottom.
  **Files**: `src/Grove/Views/SidebarView.axaml`, `src/Grove/Views/SidebarView.axaml.cs`
  **Structure**:
  ```xml
  <DockPanel>
    <!-- Header: Grove logo + settings gear -->
    <Border DockPanel.Dock="Top" Padding="16,12">
      <Grid ColumnDefinitions="Auto,*,Auto">
        <PathIcon Data="..." /> <!-- tree icon -->
        <TextBlock Text="grove" FontWeight="Bold" FontSize="18" />
        <Button Command="{Binding OpenSettingsCommand}"> <!-- gear icon --> </Button>
      </Grid>
    </Border>
    
    <!-- Footer: add buttons -->
    <StackPanel DockPanel.Dock="Bottom" Margin="16,8">
      <Button Content="+ add root" Command="{Binding AddRootCommand}" />
    </StackPanel>
    
    <!-- Worktree tree -->
    <ScrollViewer>
      <ItemsControl ItemsSource="{Binding Roots}">
        <ItemsControl.ItemTemplate>
          <DataTemplate DataType="vm:RootViewModel">
            <!-- Root header (collapsible) -->
            <!-- Nested worktree items -->
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </DockPanel>
  ```
  **Worktree item template**: Status dot (Ellipse with color bound to Status via converter) + branch name (bold) + short path (muted, smaller font). Entire item is clickable (selects worktree).
  **Collapsible roots**: Use an `Expander` or custom toggle with `IsExpanded` binding. Root header shows repo name in uppercase (from mockup: "MY-APP").
  **Selection**: Bind `SelectedWorktree` from `MainWindowViewModel`. Highlight selected item with accent border or background.
  **Acceptance**: Roots display with nested worktrees. Clicking selects. Collapsing works. Status dots show correct colors.

- [ ] 4.4 **Create StatusDotConverter** `[S]`
  **What**: `IValueConverter` that maps `ProcessStatus` enum to a `SolidColorBrush` for the sidebar status dot.
  **Files**: `src/Grove/Converters/StatusDotConverter.cs`
  **Mapping**: `Running → Green (#4ec9b0)`, `Idle → Grey (#666680)`, `Error → Red (#f44747)`, `Starting → Amber (#dcdcaa)`
  **Acceptance**: Dots render correct colors for each status.

- [ ] 4.5 **Implement WorktreeDetailView** `[L]`
  **What**: The right panel showing header, command bar, and console for the selected worktree.
  **Files**: `src/Grove/Views/WorktreeDetailView.axaml`, `src/Grove/Views/WorktreeDetailView.axaml.cs`
  **Layout** (from mockup, top to bottom):
  ```
  ┌─────────────────────────────────────────┐
  │ Header: branch (large) + path · upstream │ [running] badge
  ├─────────────────────────────────────────┤
  │ COMMAND label                            │
  │ [command input          ] [stop][restart]│
  │ Presets: [npm run dev] [npm test] [+]   │
  ├─────────────────────────────────────────┤
  │ Console output (dark, scrollable)        │
  │ grove · main · started 4m ago            │
  │ ─────────────────────                    │
  │ ▶ vite dev                               │
  │ VITE v5.2.1 ready in 312ms              │
  │ ...                                      │
  └─────────────────────────────────────────┘
  ```
  **Header section**: `TextBlock` for branch (FontSize=24, Bold), path + upstream in muted text, status badge (Border with rounded corners, green/grey/red background + white text).
  **Command section**: `TextBox` for command input, `Button` for Stop (visible when running), `Button` for Restart (visible when running), `Button` for Run (visible when idle/error). Use `IsVisible` bindings to `CanRun`/`CanStop`. Below: `ItemsRepeater` with `WrapLayout` for preset chips (rounded `Border` + `TextBlock`, clickable).
  **Console section**: See task 4.6.
  **Acceptance**: All three zones render correctly. Buttons show/hide based on status. Presets display as chips.

- [ ] 4.6 **Implement ConsoleControl (ANSI rendering)** `[XL]`
  **What**: Custom control that renders console output with ANSI color support. This is the most complex UI component.
  **Files**:
  - `src/Grove/Controls/ConsoleControl.axaml` (UserControl)
  - `src/Grove/Controls/ConsoleControl.axaml.cs`
  - `src/Grove/Controls/ConsoleLineControl.cs` (templated control or helper)
  **Architecture**:
  - Use an `ItemsRepeater` inside a `ScrollViewer` for virtualized rendering of potentially 10K lines.
  - Each line is rendered as a `SelectableTextBlock` with `Inlines` collection populated from `AnsiSpan` data.
  - The `ItemsRepeater.ItemTemplate` creates a `SelectableTextBlock` per line.
  - A value converter (`AnsiSpansToInlinesConverter`) converts `List<AnsiSpan>` → `InlineCollection` with styled `Run` elements.
  **Key details**:
  - **Auto-scroll**: When new lines are added and user is scrolled to bottom, auto-scroll to keep at bottom. If user has scrolled up, don't auto-scroll (let them read). Detect via `ScrollViewer.Offset` vs `ScrollViewer.Extent`.
  - **Performance**: `ItemsRepeater` with `StackLayout` provides virtualization. Only visible lines are rendered.
  - **Header line**: First element is a dim "grove · {branch} · started Xm ago" text with a separator line below.
  - **Monospace font**: Use `Cascadia Mono`, `Consolas`, `monospace` font family.
  - **Background**: Dark console background (`GroveConsoleBg`).
  - **Clear button**: Top-right corner of console area, semi-transparent.
  - **Copy button**: Adjacent to clear, copies all text (stripped of ANSI) to clipboard.
  **Acceptance**: Console renders colored output. Auto-scroll works. 10K lines don't cause lag. Clear/copy work.

- [ ] 4.7 **Create AnsiSpansToInlinesConverter** `[M]`
  **What**: Avalonia `IValueConverter` that takes a `List<AnsiSpan>` and produces `InlineCollection` for a `SelectableTextBlock`.
  **Files**: `src/Grove/Converters/AnsiSpansToInlinesConverter.cs`
  **Implementation**:
  ```csharp
  public object Convert(object value, ...)
  {
      if (value is not List<AnsiSpan> spans) return new InlineCollection();
      var inlines = new InlineCollection();
      foreach (var span in spans)
      {
          var run = new Run(span.Text);
          if (span.Foreground is { } fg)
              run.Foreground = new SolidColorBrush(Color.FromRgb(fg.R, fg.G, fg.B));
          if (span.Bold) run.FontWeight = FontWeight.Bold;
          if (span.Italic) run.FontStyle = FontStyle.Italic;
          if (span.Underline) run.TextDecorations = TextDecorations.Underline;
          inlines.Add(run);
      }
      return inlines;
  }
  ```
  **Note**: Avalonia's `SelectableTextBlock` supports `Inlines` property. Verify this works with binding (may need a custom attached property or code-behind approach if direct binding to `Inlines` isn't supported — in that case, use a behavior or custom control that sets Inlines in code).
  **Acceptance**: Colored text renders in console lines.

- [ ] 4.8 **Implement SettingsView** `[M]`
  **What**: Settings page with sections for global defaults, roots manager, presets manager, per-worktree overrides, and appearance.
  **Files**: `src/Grove/Views/SettingsView.axaml`, `src/Grove/Views/SettingsView.axaml.cs`
  **Layout**: Scrollable single-column form with section headers.
  **Sections**:
  1. **Global Defaults**: TextBox for default command, ToggleSwitch for auto-start.
  2. **Roots Manager**: `ItemsControl` listing roots. Each row: path (TextBlock), mode toggle (repo/scan `ComboBox`), remove button. "Add root" button at bottom.
  3. **Presets Manager**: `ItemsControl` listing presets. Each row: name TextBox, command TextBox, remove button. "Add preset" button.
  4. **Per-Worktree Overrides**: `ItemsControl` listing overrides. Each row: worktree path (read-only), command TextBox, env vars (nested `DataGrid` or `ItemsControl` of key-value rows).
  5. **Appearance**: Theme selector (`ComboBox`: Light/Dark/System), console font size (`NumericUpDown` or `Slider`).
  **Acceptance**: All settings editable and persisted. Theme change applies live.

- [ ] 4.9 **Implement theme switching** `[M]`
  **What**: Support Light/Dark/System theme modes. Load the appropriate resource dictionary based on the setting. For "System", detect OS theme preference.
  **Files**: `src/Grove/App.axaml.cs` (theme loading logic), `src/Grove/Styles/DarkTheme.axaml`, `src/Grove/Styles/LightTheme.axaml`
  **Implementation**:
  - Use Avalonia's `RequestedThemeVariant` on `Application` — set to `ThemeVariant.Dark`, `ThemeVariant.Light`, or `ThemeVariant.Default` (system).
  - Override specific resources in each theme dictionary for Grove-specific colors.
  - When theme setting changes in SettingsViewModel, update `Application.Current.RequestedThemeVariant`.
  **Acceptance**: Switching between Light/Dark/System works. All custom colors adapt.

- [ ] 4.10 **Style all controls to match mockup** `[M]`
  **What**: Apply custom styles to buttons, text inputs, borders, etc. to match the dark terminal aesthetic from the mockup. Rounded corners on buttons and inputs, subtle borders, proper spacing.
  **Files**: `src/Grove/Styles/Base.axaml`
  **Key styles**:
  - Buttons: rounded corners (4px), subtle border, hover effect
  - TextBox (command input): dark background, light text, rounded corners, larger font
  - Preset chips: pill-shaped borders, muted background, hover highlight
  - Status badge: small rounded pill with colored background + white text
  - Sidebar items: hover highlight, selected state with accent left border
  - Section headers: uppercase, muted, small font, letter-spacing
  **Acceptance**: Visual match to mockup. Consistent styling across all views.

- [ ] 4.11 **Add "empty state" and "no selection" views** `[S]`
  **What**: When no worktree is selected, show a centered message in the detail panel ("Select a worktree to get started"). When a root has no worktrees, show a message in the sidebar group. When no roots are configured (first launch), show a welcome/onboarding prompt.
  **Files**: `src/Grove/Views/EmptyStateView.axaml`, `src/Grove/Views/WelcomeView.axaml`
  **Acceptance**: Empty states display appropriately.

---

### Phase 5: Process & Console Integration

- [ ] 5.1 **Wire ProcessManager to ViewModels** `[M]`
  **What**: Connect `IProcessManager` events to ViewModel property updates. Ensure all UI updates happen on the UI thread via `Dispatcher.UIThread`.
  **Files**: `src/Grove/ViewModels/WorktreeDetailViewModel.cs`, `src/Grove/ViewModels/WorktreeViewModel.cs`
  **Key wiring**:
  - `ProcessManager.StatusChanged` → update `WorktreeViewModel.Status` and `WorktreeDetailViewModel.Status`
  - `ConsoleOutputManager.LineAdded` → parse through `AnsiParser`, add `ConsoleLine` to `WorktreeDetailViewModel.ConsoleLines`
  - All event handlers must use `Dispatcher.UIThread.InvokeAsync(...)` for thread safety.
  **Acceptance**: Starting a process updates sidebar dot and detail panel status badge simultaneously.

- [ ] 5.2 **Implement Run/Stop/Restart command logic** `[M]`
  **What**: Implement the actual command execution flow in `WorktreeDetailViewModel`.
  **Files**: `src/Grove/ViewModels/WorktreeDetailViewModel.cs`
  **Run flow**:
  1. Save current command to config
  2. Set status to `Starting`
  3. Call `ProcessManager.StartProcess(worktreePath, command, envVars)`
  4. Status updates to `Running` via event
  **Stop flow**:
  1. Call `ProcessManager.StopProcessAsync(worktreePath)` with 5s grace period
  2. Status updates to `Idle` via event
  **Restart flow**:
  1. Stop (await completion)
  2. Clear console (optional — configurable)
  3. Run
  **Edge cases**:
  - Empty command → show validation error, don't run
  - Process exits on its own → status updates automatically
  - Double-click prevention → disable button while operation in progress
  **Acceptance**: Full run/stop/restart cycle works. Status transitions are correct.

- [ ] 5.3 **Implement console auto-scroll behavior** `[M]`
  **What**: Create an attached behavior for the console `ScrollViewer` that auto-scrolls to bottom when new content is added, but only if the user hasn't scrolled up.
  **Files**: `src/Grove/Controls/AutoScrollBehavior.cs`
  **Implementation**:
  ```csharp
  public class AutoScrollBehavior : AvaloniaObject
  {
      public static readonly AttachedProperty<bool> EnabledProperty = ...;
      // On attached: subscribe to ScrollViewer.ScrollChanged
      // Track if user is "at bottom" (within ~20px tolerance)
      // When items change and user was at bottom, scroll to end
  }
  ```
  **Alternative**: Handle in `ConsoleControl` code-behind by subscribing to collection changes and checking scroll position.
  **Acceptance**: New output scrolls into view. Scrolling up pauses auto-scroll. Scrolling back to bottom resumes.

- [ ] 5.4 **Implement elapsed time display** `[S]`
  **What**: Show "started Xm ago" in the console header, updating every minute.
  **Files**: `src/Grove/ViewModels/WorktreeDetailViewModel.cs`
  **Implementation**: Use a `DispatcherTimer` with 60s interval. Compute elapsed from `ManagedProcess.StartedAt`. Format as "started Xs ago", "started Xm ago", "started Xh Ym ago". Stop timer when process stops.
  **Acceptance**: Timer updates. Shows correct elapsed time.

- [ ] 5.5 **Implement clipboard copy for console** `[S]`
  **What**: Copy all console text (plain text, ANSI stripped) to clipboard.
  **Files**: `src/Grove/ViewModels/WorktreeDetailViewModel.cs`
  **Implementation**: Iterate `ConsoleLines`, join span texts, use `TopLevel.Clipboard.SetTextAsync()`.
  **Acceptance**: Copied text is plain (no ANSI codes) and matches console content.

- [ ] 5.6 **Implement preset management in command bar** `[S]`
  **What**: Clicking a preset chip loads its command into the command TextBox. "+ add preset" chip opens a small dialog/flyout to create a new preset from the current command.
  **Files**: `src/Grove/ViewModels/WorktreeDetailViewModel.cs`, `src/Grove/Views/WorktreeDetailView.axaml`
  **Acceptance**: Clicking preset fills command. Adding preset saves to config and appears in strip.

---

### Phase 6: System Tray & Platform

- [ ] 6.1 **Implement TrayIcon in App.axaml** `[M]`
  **What**: Define the system tray icon with a context menu. The tray icon shows aggregate process status.
  **Files**: `src/Grove/App.axaml`, `src/Grove/App.axaml.cs`
  **AXAML**:
  ```xml
  <TrayIcon.Icons>
    <TrayIcons>
      <TrayIcon Icon="/Assets/grove-icon.ico"
                ToolTipText="Grove"
                Command="{Binding ShowWindowCommand}">
        <TrayIcon.Menu>
          <NativeMenu>
            <!-- Dynamic: list of running processes -->
            <NativeMenuItem Header="Show Grove" Command="{Binding ShowWindowCommand}" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Quit" Command="{Binding QuitCommand}" />
          </NativeMenu>
        </TrayIcon.Menu>
      </TrayIcon>
    </TrayIcons>
  </TrayIcon.Icons>
  ```
  **Behavior**:
  - Clicking tray icon shows/focuses the window.
  - Context menu lists running processes (worktree branch names) with stop option.
  - "Quit" stops all processes and exits.
  - Tray icon color/state: ideally swap icon based on aggregate status (green/red/grey). If icon swapping is complex, use ToolTipText to indicate status.
  **Acceptance**: Tray icon appears. Context menu works. Click shows window.

- [ ] 6.2 **Implement window close → minimize to tray** `[M]`
  **What**: Override window close behavior. Instead of exiting, hide the window and keep running in the background. Only truly exit via tray "Quit" or when no processes are running.
  **Files**: `src/Grove/MainWindow.axaml.cs`
  **Implementation**:
  ```csharp
  protected override void OnClosing(WindowClosingEventArgs e)
  {
      if (_processManager.Processes.Any(p => p.Value.Status == ProcessStatus.Running))
      {
          e.Cancel = true;
          this.Hide();
          // Optionally show a notification: "Grove is still running in the tray"
      }
      else
      {
          // No running processes — actually close
          base.OnClosing(e);
      }
  }
  ```
  **Acceptance**: Closing window with running processes hides to tray. Closing with no processes exits.

- [ ] 6.3 **Implement Quit command (stop all + exit)** `[S]`
  **What**: When user clicks "Quit" from tray, stop all running processes gracefully, then exit the application.
  **Files**: `src/Grove/App.axaml.cs` or `src/Grove/ViewModels/TrayViewModel.cs`
  **Implementation**:
  ```csharp
  [RelayCommand]
  private async Task QuitAsync()
  {
      _processManager.StopAll();
      // Wait briefly for graceful shutdown
      await Task.Delay(1000);
      // Force kill any remaining
      Environment.Exit(0);
  }
  ```
  **Acceptance**: All processes stop. App exits cleanly.

- [ ] 6.4 **Platform-specific shell execution** `[S]`
  **What**: Ensure `ProcessManager` uses the correct shell for the platform. Already designed in 2.4 but verify and test.
  **Files**: `src/Grove.Core/Services/ProcessManager.cs`
  **Details**:
  - Windows: `FileName = "cmd.exe"`, `Arguments = $"/c {command}"`
  - Unix: `FileName = "/bin/sh"`, `Arguments = $"-c \"{command}\""`
  - Detect via `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`
  **Acceptance**: Commands execute correctly on Windows. (Unix testing deferred to CI or manual.)

- [ ] 6.5 **Platform-specific process termination** `[M]`
  **What**: Ensure process tree kill works correctly on both platforms.
  **Files**: `src/Grove.Core/Services/ProcessManager.cs`
  **Details**:
  - .NET's `Process.Kill(entireProcessTree: true)` should work on both platforms (.NET 10).
  - Graceful stop: on Windows, try `GenerateConsoleCtrlEvent` (Ctrl+C) first via P/Invoke, fall back to Kill. On Unix, `Process.Kill()` sends SIGTERM by default (verify), then SIGKILL after timeout.
  - Simpler approach for v1: just use `Process.Kill(entireProcessTree: true)` with a grace period. Ctrl+C signal is a v2 enhancement.
  **Acceptance**: Stopping a process kills the entire process tree (e.g., npm → node child processes).

- [ ] 6.6 **Implement "Add Worktree" dialog** `[M]`
  **What**: The "+ add worktree" button in the sidebar opens a dialog to create a new git worktree. User enters branch name and optionally a base branch. Runs `git worktree add`.
  **Files**:
  - `src/Grove/Views/AddWorktreeDialog.axaml`
  - `src/Grove/Views/AddWorktreeDialog.axaml.cs`
  - `src/Grove/ViewModels/AddWorktreeViewModel.cs`
  **Dialog fields**: Branch name (TextBox), base branch (ComboBox with existing branches), path (auto-generated or custom).
  **Acceptance**: Creating a worktree via dialog adds it to the sidebar after refresh.

- [ ] 6.7 **Implement "Add Root" folder picker** `[S]`
  **What**: The "+ add root" button opens the system folder picker. Selected folder is added to config as a new root (default mode: repo). If the folder doesn't contain `.git`, prompt to use scan mode.
  **Files**: `src/Grove/ViewModels/MainWindowViewModel.cs` (or `SettingsViewModel.cs`)
  **Implementation**: Use Avalonia's `IStorageProvider.OpenFolderPickerAsync()`.
  **Acceptance**: Folder picker opens. Selected folder appears as new root in sidebar.

- [ ] 6.8 **Handle first-launch experience** `[S]`
  **What**: On first launch (no config file exists), show a welcome state prompting the user to add their first root. The sidebar shows the welcome message instead of an empty list.
  **Files**: `src/Grove/Views/WelcomeView.axaml`, `src/Grove/ViewModels/MainWindowViewModel.cs`
  **Acceptance**: First launch shows welcome prompt. After adding a root, normal UI appears.

- [ ] 6.9 **Dynamic tray menu with running processes** `[M]`
  **What**: The tray context menu dynamically lists all currently running processes (branch name + status). Each entry can be clicked to show that worktree in the main window.
  **Files**: `src/Grove/App.axaml.cs`
  **Implementation**: Build `NativeMenu` items programmatically in code-behind, updating when process status changes. Avalonia's `NativeMenu` supports dynamic items.
  **Acceptance**: Running processes appear in tray menu. Clicking one opens the window and selects that worktree.

- [ ] 6.10 **Final integration testing & polish** `[L]`
  **What**: End-to-end testing of the complete application. Fix visual bugs, alignment issues, edge cases.
  **Checklist**:
  - [ ] Add a real git repo root → worktrees discovered and listed
  - [ ] Add a scan-mode root → nested repos discovered
  - [ ] Run `npm run dev` (or similar) → live output streams with colors
  - [ ] Stop process → status updates, process tree killed
  - [ ] Restart process → clean restart
  - [ ] Switch between worktrees → detail panel updates, console shows correct output
  - [ ] Add/edit/remove presets → persisted and reflected in command bar
  - [ ] Per-worktree env vars → passed to spawned process
  - [ ] Close window → tray icon, processes persist
  - [ ] Reopen from tray → window shows, state preserved
  - [ ] Quit from tray → all processes stopped, app exits
  - [ ] Theme switching (dark/light/system)
  - [ ] Console with 10K+ lines → no lag (virtualization works)
  - [ ] Config survives restart (kill and relaunch)
  **Acceptance**: All checklist items pass.

---

## Verification

- [ ] `dotnet build Grove.sln` succeeds with no warnings (treat warnings as errors)
- [ ] `dotnet test` — all unit tests pass (ConfigService, RingBuffer, AnsiParser, GitService parsing)
- [ ] App launches on Windows without errors
- [ ] Full workflow: add root → discover worktrees → run command → see output → stop → close to tray → quit
- [ ] No memory leaks on long-running processes (check with dotnet-counters)
- [ ] Console handles 10,000 lines without UI freeze

---

## Dependency Graph

```
Phase 1 (Scaffolding)
  └─► Phase 2 (Core Services) — models first, then services
        ├─► Phase 3 (ViewModels) — depends on service interfaces
        │     └─► Phase 4 (Views) — depends on ViewModels
        │           └─► Phase 5 (Process Integration) — wires everything together
        └─► Phase 5 also depends directly on Phase 2 services
              └─► Phase 6 (Tray & Platform) — final layer, depends on all above
```

Within phases, the task numbering reflects internal dependencies (e.g., 2.1 models before 2.2 config service).

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| ANSI rendering performance with 10K lines | High | Use `ItemsRepeater` virtualization. Profile early. Fall back to plain text if needed. |
| `SelectableTextBlock.Inlines` not bindable | Medium | Use code-behind or attached property to set Inlines programmatically instead of binding. |
| Process tree kill not working on Windows | High | Test with `npm run dev` (spawns child node process). Use `Process.Kill(entireProcessTree: true)`. Fall back to `taskkill /T /F /PID`. |
| Avalonia TrayIcon not working on all Linux DEs | Low | Document supported platforms. TrayIcon is optional — app still works without it. |
| .NET 10 + Avalonia compatibility | Medium | Use latest Avalonia 11.x stable. Test early in Phase 1. |
| Large scan-mode roots (thousands of repos) | Medium | Limit scan depth to 3. Add progress indicator. Make scan async with cancellation. |
