# Grove — Full Implementation Plan

## TL;DR
> **Summary**: Build Grove, a desktop git worktree manager with Avalonia UI (.NET 10), ReactiveUI MVVM, per-worktree process execution, live ANSI console output, and system tray persistence — from zero to shipping v1.
> **Estimated Effort**: XL (6 phases, ~60 tasks)

## Context

### Original Request
Build the complete Grove application as described in GOAL.md: a two-panel desktop app (sidebar + detail) that discovers git worktrees across configured roots, lets users run/stop/restart commands per worktree, streams live console output with ANSI color support, and persists to the system tray when closed.

### Key Findings
- **Template**: `dotnet new avalonia.mvvm -m ReactiveUI` is available and generates a ReactiveUI-based Avalonia project. Default Avalonia version is 11.2.1. Template targets net9.0 max — we must manually retarget to `net10.0`.
- **.NET 10.0.200 SDK** is installed and ready.
- **No code exists** — completely greenfield.
- **UI mockup** (image.png) shows: dark sidebar with repo group headers, worktree entries with colored status dots + branch name + path, detail panel with branch header/status badge, command bar with stop/restart buttons, preset chips, and a dark terminal-style console output area.
- **Data model** is JSON-based, stored at `%APPDATA%\grove\config.json` (Windows) / `~/.config/grove/config.json` (Unix).
- **Process management** requires: spawn via platform shell, capture stdout/stderr as Rx streams, track exit codes, support stop/force-kill/restart, one process per worktree max.
- **Console**: 10,000-line ring buffer per worktree, ANSI color parsing, monospace rendering.

### Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Solution structure | `Grove.sln` → `src/Grove` (app) + `src/Grove.Core` (library) | Separation of concerns; Core has no UI dependency, testable in isolation |
| MVVM framework | **ReactiveUI** with `Avalonia.ReactiveUI` | Rx-native commands, OAPH, DynamicData for collections — NOT CommunityToolkit.Mvvm |
| DI container | `Microsoft.Extensions.DependencyInjection` | Standard .NET DI; wired into ReactiveUI's Locator if needed |
| Reactive collections | DynamicData `SourceCache<T,TKey>` / `SourceList<T>` | Filtering, sorting, grouping, thread-safe binding to UI |
| Git integration | Shell out to `git` CLI, parse porcelain output | No libgit2 dependency |
| Process I/O | `System.Diagnostics.Process` wrapped in Rx observables | Output as `IObservable<string>`, status as `IObservable<ProcessStatus>` |
| Config persistence | `System.Text.Json` with source generators | AOT-friendly, fast, no reflection |
| Console rendering | Custom `AnsiParser` → styled `ConsoleLine` spans, `ItemsRepeater` for virtualized display | DynamicData `SourceList<ConsoleLine>` as ring buffer |
| Views | `ReactiveWindow<TViewModel>` / `ReactiveUserControl<TViewModel>` | Type-safe bindings, ReactiveUI integration |
| Thread marshalling | `ObserveOn(RxApp.MainThreadScheduler)` | NOT `Dispatcher.UIThread` — keep it Rx-idiomatic |

## Objectives

### Core Objective
Deliver a fully functional v1 of Grove that matches the GOAL.md spec: worktree discovery, per-worktree command execution, live console output, system tray persistence, and settings management.

### Deliverables
- [x] Solution with two projects (`Grove`, `Grove.Core`) building on .NET 10
- [x] Git worktree discovery (repo mode + scan mode)
- [x] Sidebar with collapsible repo groups and worktree entries with status indicators
- [x] Detail panel with header, command bar, preset chips, and live console
- [x] Process management: run/stop/restart with Rx-based I/O streaming
- [x] ANSI color parsing and virtualized console rendering
- [x] JSON config persistence with per-worktree overrides and global presets
- [x] Settings page (roots, presets, defaults, appearance)
- [x] System tray integration with aggregate status icon
- [x] Light/dark/system theme support
- [x] Platform-aware shell execution (Windows `cmd /c`, Unix `sh -c`)

### Definition of Done
- [x] `dotnet build src/Grove` succeeds with zero warnings (TreatWarningsAsErrors)
- [x] App launches, discovers worktrees from a configured root, displays them in sidebar
- [x] Selecting a worktree shows detail panel; typing a command and clicking Run starts the process
- [x] Console output streams in real-time with ANSI colors rendered
- [x] Stop/Restart buttons work; status dots update correctly
- [x] Closing the window minimizes to tray; tray icon shows aggregate status
- [x] Settings page allows adding/removing roots, editing presets, changing theme
- [x] Config persists across app restarts

### Guardrails (Must NOT)
- **No CommunityToolkit.Mvvm** — ReactiveUI only, everywhere
- **No libgit2** — git CLI only
- **No Electron/web** — Avalonia native only
- **No filesystem watcher** — v2 feature
- **No CLI tool** (`grove` command) — v2 feature
- **No `[ObservableProperty]`** or `[RelayCommand]` attributes — these are CommunityToolkit patterns
- **No `Dispatcher.UIThread`** — use `RxApp.MainThreadScheduler`
- **No `ObservableCollection<T>`** directly — use DynamicData `SourceCache`/`SourceList` → `.Bind(out _readOnlyCollection)`

---

## TODOs

### Phase 1: Project Scaffolding

- [x] **1.1 Create solution and app project** `[M]`
  **What**: Generate the Avalonia MVVM app from template with ReactiveUI, create the solution file, retarget to `net10.0`.
  **Files**:
  - `Grove.sln` (new)
  - `src/Grove/Grove.csproj` (new, from template, then modified)
  - `src/Grove/App.axaml` (new, from template)
  - `src/Grove/App.axaml.cs` (new, from template)
  - `src/Grove/Program.cs` (new, from template)
  - `src/Grove/ViewModels/MainWindowViewModel.cs` (new, from template — will be replaced later)
  - `src/Grove/Views/MainWindow.axaml` (new, from template)
  - `src/Grove/Views/MainWindow.axaml.cs` (new, from template)
  **Key code**:
  ```bash
  # Generate from template into src/Grove
  dotnet new avalonia.mvvm -n Grove -o src/Grove -m ReactiveUI --framework net9.0
  # Create solution at repo root
  dotnet new sln -n Grove
  dotnet sln add src/Grove/Grove.csproj
  ```
  Then manually edit `Grove.csproj` to retarget:
  ```xml
  <TargetFramework>net10.0</TargetFramework>
  ```
  **Acceptance**: `dotnet build src/Grove` succeeds; `dotnet run --project src/Grove` launches an empty Avalonia window.

- [x] **1.2 Create Grove.Core class library** `[S]`
  **What**: Create the core library project with no UI dependencies. This holds models, services, interfaces, and config logic.
  **Files**:
  - `src/Grove.Core/Grove.Core.csproj` (new)
  **Key code**:
  ```bash
  dotnet new classlib -n Grove.Core -o src/Grove.Core --framework net10.0
  dotnet sln add src/Grove.Core/Grove.Core.csproj
  dotnet add src/Grove/Grove.csproj reference src/Grove.Core/Grove.Core.csproj
  ```
  Add to `Grove.Core.csproj`:
  ```xml
  <ItemGroup>
    <PackageReference Include="System.Reactive" Version="6.*" />
    <PackageReference Include="DynamicData" Version="9.*" />
  </ItemGroup>
  ```
  **Acceptance**: `dotnet build src/Grove.Core` succeeds; Grove app project references Core.

- [x] **1.3 Add NuGet packages** `[S]`
  **What**: Add all required NuGet packages to both projects.
  **Files**:
  - `src/Grove/Grove.csproj` (modify — add packages)
  - `src/Grove.Core/Grove.Core.csproj` (modify — add packages)
  **Key code**:
  Grove app packages (some come from template, verify and add missing):
  ```xml
  <!-- src/Grove/Grove.csproj -->
  <PackageReference Include="Avalonia" Version="11.2.*" />
  <PackageReference Include="Avalonia.Desktop" Version="11.2.*" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.*" />
  <PackageReference Include="Avalonia.ReactiveUI" Version="11.2.*" />
  <PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.*" />
  <PackageReference Include="Avalonia.Diagnostics" Version="11.2.*" Condition="'$(Configuration)' == 'Debug'" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.*" />
  <PackageReference Include="DynamicData" Version="9.*" />
  ```
  Grove.Core packages:
  ```xml
  <!-- src/Grove.Core/Grove.Core.csproj -->
  <PackageReference Include="System.Reactive" Version="6.*" />
  <PackageReference Include="DynamicData" Version="9.*" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.*" />
  ```
  **Acceptance**: `dotnet restore` succeeds for both projects; `dotnet build` succeeds.

- [x] **1.4 Configure DI container and wire into ReactiveUI** `[M]`
  **What**: Set up `Microsoft.Extensions.DependencyInjection` as the app's DI container. Create a `Bootstrapper` class that builds the `ServiceProvider` and registers all services/viewmodels. Wire it so ReactiveUI's `Locator` can resolve view-viewmodel pairs (needed for `ViewLocator`).
  **Files**:
  - `src/Grove/Bootstrapper.cs` (new)
  - `src/Grove/App.axaml.cs` (modify)
  **Key code**:
  ```csharp
  // Bootstrapper.cs
  public static class Bootstrapper
  {
      public static IServiceProvider Build()
      {
          var services = new ServiceCollection();

          // Core services
          services.AddSingleton<IGitService, GitService>();
          services.AddSingleton<IConfigService, ConfigService>();
          services.AddSingleton<IProcessManager, ProcessManager>();
          services.AddSingleton<IShellService, ShellService>();

          // ViewModels
          services.AddSingleton<MainWindowViewModel>();
          services.AddTransient<SettingsViewModel>();

          return services.BuildServiceProvider();
      }
  }

  // App.axaml.cs — in OnFrameworkInitializationCompleted
  public override void OnFrameworkInitializationCompleted()
  {
      var provider = Bootstrapper.Build();

      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      {
          desktop.MainWindow = new MainWindow
          {
              DataContext = provider.GetRequiredService<MainWindowViewModel>()
          };
      }
      base.OnFrameworkInitializationCompleted();
  }
  ```
  **Acceptance**: App launches with DI-resolved MainWindowViewModel; services are injectable.

- [x] **1.5 Set up project structure and folders** `[S]`
  **What**: Create the folder structure for both projects following conventions.
  **Files**:
  ```
  src/Grove/
    Assets/
    Converters/
    Styles/
    Views/
    ViewModels/
  src/Grove.Core/
    Models/
    Services/
    Services/Abstractions/
  ```
  **Key code**: N/A — just directory creation and placeholder files if needed.
  **Acceptance**: Folder structure exists; solution builds.

- [x] **1.6 Configure global build settings** `[S]`
  **What**: Add `Directory.Build.props` at repo root for shared build settings: nullable enable, implicit usings, warnings as errors, and a `global.json` to pin SDK version.
  **Files**:
  - `Directory.Build.props` (new)
  - `global.json` (new)
  **Key code**:
  ```xml
  <!-- Directory.Build.props -->
  <Project>
    <PropertyGroup>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
      <WarningLevel>9999</WarningLevel>
    </PropertyGroup>
  </Project>
  ```
  ```json
  // global.json
  {
    "sdk": {
      "version": "10.0.200",
      "rollForward": "latestFeature"
    }
  }
  ```
  **Acceptance**: `dotnet build` uses .NET 10 SDK; warnings are errors.

- [x] **1.7 Add .gitignore and initial commit** `[S]`
  **What**: Add a .NET-appropriate `.gitignore` (bin, obj, .vs, etc.) and verify the project compiles cleanly.
  **Files**:
  - `.gitignore` (new or update existing)
  **Key code**:
  ```bash
  dotnet new gitignore
  ```
  **Acceptance**: `git status` is clean after build; no bin/obj tracked.

---

### Phase 2: Core Services — No UI

- [x] **2.1 Define data models** `[M]`
  **What**: Create all domain models in `Grove.Core/Models/`. These are plain C# records/classes matching the GOAL.md data model. Models are serialization-friendly (System.Text.Json).
  **Files**:
  - `src/Grove.Core/Models/RootConfig.cs` (new)
  - `src/Grove.Core/Models/RootMode.cs` (new — enum: Repo, Scan)
  - `src/Grove.Core/Models/WorktreeInfo.cs` (new)
  - `src/Grove.Core/Models/WorktreeConfig.cs` (new)
  - `src/Grove.Core/Models/CommandPreset.cs` (new)
  - `src/Grove.Core/Models/GroveConfig.cs` (new)
  - `src/Grove.Core/Models/ProcessStatus.cs` (new — enum: Idle, Starting, Running, Error, Stopped)
  - `src/Grove.Core/Models/AppTheme.cs` (new — enum: Light, Dark, System)
  **Key code**:
  ```csharp
  // RootConfig.cs
  public sealed class RootConfig
  {
      public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
      public string Path { get; set; } = string.Empty;
      public RootMode Mode { get; set; } = RootMode.Repo;
  }

  // WorktreeInfo.cs — parsed from git output, not persisted
  public sealed record WorktreeInfo(
      string Path,
      string HeadCommit,
      string BranchName,
      bool IsBare,
      string RepoRootPath  // which root repo this belongs to
  );

  // WorktreeConfig.cs — persisted per-worktree settings
  public sealed class WorktreeConfig
  {
      public string? Command { get; set; }
      public Dictionary<string, string> Env { get; set; } = new();
  }

  // GroveConfig.cs — top-level config file
  public sealed class GroveConfig
  {
      public List<RootConfig> Roots { get; set; } = new();
      public string DefaultCommand { get; set; } = string.Empty;
      public bool AutoStart { get; set; }
      public List<CommandPreset> Presets { get; set; } = new();
      public Dictionary<string, WorktreeConfig> Worktrees { get; set; } = new();
      public AppTheme Theme { get; set; } = AppTheme.Dark;
      public int ConsoleFontSize { get; set; } = 13;
  }

  // CommandPreset.cs
  public sealed class CommandPreset
  {
      public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
      public string Name { get; set; } = string.Empty;
      public string Command { get; set; } = string.Empty;
  }
  ```
  **Acceptance**: Models compile; can be serialized/deserialized with System.Text.Json.

- [x] **2.2 Config service with JSON source generators** `[M]`
  **What**: Implement `IConfigService` / `ConfigService` that loads/saves `GroveConfig` from the platform-appropriate config path. Use `System.Text.Json` source generators for AOT-friendly serialization. Config is loaded once at startup and saved on every mutation.
  **Files**:
  - `src/Grove.Core/Services/Abstractions/IConfigService.cs` (new)
  - `src/Grove.Core/Services/ConfigService.cs` (new)
  - `src/Grove.Core/Services/GroveJsonContext.cs` (new — source generator)
  **Key code**:
  ```csharp
  // IConfigService.cs
  public interface IConfigService
  {
      GroveConfig Config { get; }
      Task LoadAsync(CancellationToken ct = default);
      Task SaveAsync(CancellationToken ct = default);
      string ConfigDirectory { get; }
  }

  // GroveJsonContext.cs
  [JsonSerializable(typeof(GroveConfig))]
  [JsonSourceGenerationOptions(
      WriteIndented = true,
      PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
  public partial class GroveJsonContext : JsonSerializerContext { }

  // ConfigService.cs
  public sealed class ConfigService : IConfigService
  {
      public string ConfigDirectory { get; } = Path.Combine(
          Environment.GetFolderPath(
              OperatingSystem.IsWindows()
                  ? Environment.SpecialFolder.ApplicationData
                  : Environment.SpecialFolder.UserProfile),
              OperatingSystem.IsWindows() ? "grove" : ".config/grove");

      private string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

      public GroveConfig Config { get; private set; } = new();

      public async Task LoadAsync(CancellationToken ct = default)
      {
          if (!File.Exists(ConfigPath)) return;
          await using var stream = File.OpenRead(ConfigPath);
          Config = await JsonSerializer.DeserializeAsync(
              stream, GroveJsonContext.Default.GroveConfig, ct) ?? new();
      }

      public async Task SaveAsync(CancellationToken ct = default)
      {
          Directory.CreateDirectory(ConfigDirectory);
          await using var stream = File.Create(ConfigPath);
          await JsonSerializer.SerializeAsync(
              stream, Config, GroveJsonContext.Default.GroveConfig, ct);
      }
  }
  ```
  **Acceptance**: Config round-trips: save → load → verify equality. Platform path is correct on Windows.

- [x] **2.3 Shell service (platform-aware)** `[S]`
  **What**: Implement `IShellService` that provides platform-appropriate shell invocation details and process start info creation.
  **Files**:
  - `src/Grove.Core/Services/Abstractions/IShellService.cs` (new)
  - `src/Grove.Core/Services/ShellService.cs` (new)
  **Key code**:
  ```csharp
  public interface IShellService
  {
      ProcessStartInfo CreateStartInfo(string command, string workingDirectory,
          Dictionary<string, string>? envOverrides = null);
  }

  public sealed class ShellService : IShellService
  {
      public ProcessStartInfo CreateStartInfo(string command, string workingDirectory,
          Dictionary<string, string>? envOverrides = null)
      {
          var isWindows = OperatingSystem.IsWindows();
          var psi = new ProcessStartInfo
          {
              FileName = isWindows ? "cmd.exe" : "/bin/sh",
              Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
              WorkingDirectory = workingDirectory,
              RedirectStandardOutput = true,
              RedirectStandardError = true,
              RedirectStandardInput = false,
              UseShellExecute = false,
              CreateNoWindow = true,
          };

          if (envOverrides is not null)
          {
              foreach (var (key, value) in envOverrides)
                  psi.Environment[key] = value;
          }

          return psi;
      }
  }
  ```
  **Acceptance**: `CreateStartInfo("echo hello", "/tmp")` returns correct PSI for current platform.

- [x] **2.4 Git service — worktree discovery** `[L]`
  **What**: Implement `IGitService` / `GitService` that discovers worktrees. Supports both repo mode (run `git worktree list --porcelain` in a single repo) and scan mode (find all `.git` dirs under a parent, then list worktrees for each). Parse porcelain output into `WorktreeInfo` records.
  **Files**:
  - `src/Grove.Core/Services/Abstractions/IGitService.cs` (new)
  - `src/Grove.Core/Services/GitService.cs` (new)
  **Key code**:
  ```csharp
  public interface IGitService
  {
      Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(string repoPath, CancellationToken ct = default);
      Task<IReadOnlyList<string>> DiscoverReposAsync(string scanPath, CancellationToken ct = default);
      Task<WorktreeInfo?> AddWorktreeAsync(string repoPath, string branchName, string? path = null, CancellationToken ct = default);
      Task<string?> GetUpstreamBranchAsync(string worktreePath, CancellationToken ct = default);
  }

  // GitService.cs — parsing git worktree list --porcelain
  // Example porcelain output:
  // worktree /path/to/main
  // HEAD abc123
  // branch refs/heads/main
  //
  // worktree /path/to/feature
  // HEAD def456
  // branch refs/heads/feat/auth
  //
  public sealed class GitService : IGitService
  {
      private readonly IShellService _shell;

      public async Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(
          string repoPath, CancellationToken ct = default)
      {
          var psi = new ProcessStartInfo("git", "worktree list --porcelain")
          {
              WorkingDirectory = repoPath,
              RedirectStandardOutput = true,
              UseShellExecute = false,
              CreateNoWindow = true,
          };
          using var proc = Process.Start(psi)!;
          var output = await proc.StandardOutput.ReadToEndAsync(ct);
          await proc.WaitForExitAsync(ct);
          return ParsePorcelainOutput(output, repoPath);
      }

      private static List<WorktreeInfo> ParsePorcelainOutput(string output, string repoRoot)
      {
          var results = new List<WorktreeInfo>();
          var blocks = output.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
          foreach (var block in blocks)
          {
              string? path = null, head = null, branch = null;
              bool isBare = false;
              foreach (var line in block.Split('\n'))
              {
                  if (line.StartsWith("worktree ")) path = line[9..];
                  else if (line.StartsWith("HEAD ")) head = line[5..];
                  else if (line.StartsWith("branch ")) branch = line[7..].Replace("refs/heads/", "");
                  else if (line == "bare") isBare = true;
                  else if (line == "detached") branch = "(detached)";
              }
              if (path is not null)
                  results.Add(new WorktreeInfo(path, head ?? "", branch ?? "(unknown)", isBare, repoRoot));
          }
          return results;
      }

      public async Task<IReadOnlyList<string>> DiscoverReposAsync(
          string scanPath, CancellationToken ct = default)
      {
          // Find directories containing .git (file or folder)
          var repos = new List<string>();
          foreach (var dir in Directory.EnumerateDirectories(scanPath, "*", SearchOption.AllDirectories))
          {
              ct.ThrowIfCancellationRequested();
              if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
                  repos.Add(dir);
          }
          return repos;
      }
  }
  ```
  **Acceptance**: Given a real git repo with worktrees, `GetWorktreesAsync` returns correct `WorktreeInfo` list. `DiscoverReposAsync` finds repos under a scan folder.

- [x] **2.5 Process runner with Rx observables** `[XL]`
  **What**: Implement `IProcessRunner` — the heart of Grove. Wraps `System.Diagnostics.Process` with Rx. Exposes output as `IObservable<string>`, status as `IObservable<ProcessStatus>`. Supports start, stop (graceful + force-kill after timeout), and restart. One runner instance per worktree.
  **Files**:
  - `src/Grove.Core/Services/Abstractions/IProcessRunner.cs` (new)
  - `src/Grove.Core/Services/ProcessRunner.cs` (new)
  **Key code**:
  ```csharp
  public interface IProcessRunner : IDisposable
  {
      string WorktreePath { get; }
      IObservable<string> Output { get; }
      IObservable<ProcessStatus> Status { get; }
      ProcessStatus CurrentStatus { get; }
      DateTimeOffset? StartedAt { get; }

      void Start(ProcessStartInfo psi);
      Task StopAsync(TimeSpan? gracePeriod = null);
      Task RestartAsync(ProcessStartInfo psi, TimeSpan? gracePeriod = null);
  }

  public sealed class ProcessRunner : IProcessRunner
  {
      private readonly Subject<string> _output = new();
      private readonly BehaviorSubject<ProcessStatus> _status = new(ProcessStatus.Idle);
      private Process? _process;
      private readonly CompositeDisposable _processSubscriptions = new();

      public string WorktreePath { get; }
      public IObservable<string> Output => _output.AsObservable();
      public IObservable<ProcessStatus> Status => _status.AsObservable();
      public ProcessStatus CurrentStatus => _status.Value;
      public DateTimeOffset? StartedAt { get; private set; }

      public ProcessRunner(string worktreePath)
      {
          WorktreePath = worktreePath;
      }

      public void Start(ProcessStartInfo psi)
      {
          if (_process is not null) return; // prevent double-start

          _status.OnNext(ProcessStatus.Starting);
          StartedAt = DateTimeOffset.Now;

          var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

          // Bridge process events to Rx
          var stdout = Observable.FromEventPattern<DataReceivedEventHandler, DataReceivedEventArgs>(
              h => process.OutputDataReceived += h,
              h => process.OutputDataReceived -= h)
              .Select(e => e.EventArgs.Data);

          var stderr = Observable.FromEventPattern<DataReceivedEventHandler, DataReceivedEventArgs>(
              h => process.ErrorDataReceived += h,
              h => process.ErrorDataReceived -= h)
              .Select(e => e.EventArgs.Data);

          var exited = Observable.FromEventPattern(
              h => process.Exited += h,
              h => process.Exited -= h);

          Observable.Merge(stdout, stderr)
              .Where(line => line is not null)
              .Subscribe(line => _output.OnNext(line!))
              .DisposeWith(_processSubscriptions);

          exited.Subscribe(_ =>
          {
              var exitCode = process.ExitCode;
              _status.OnNext(exitCode == 0 ? ProcessStatus.Idle : ProcessStatus.Error);
              CleanupProcess();
          }).DisposeWith(_processSubscriptions);

          process.Start();
          process.BeginOutputReadLine();
          process.BeginErrorReadLine();
          _process = process;
          _status.OnNext(ProcessStatus.Running);
      }

      public async Task StopAsync(TimeSpan? gracePeriod = null)
      {
          if (_process is null || _process.HasExited) return;
          var grace = gracePeriod ?? TimeSpan.FromSeconds(5);

          // Platform-aware graceful shutdown
          if (OperatingSystem.IsWindows())
          {
              // taskkill sends CTRL_C_EVENT or terminates the process tree
              try
              {
                  using var killer = Process.Start(new ProcessStartInfo("taskkill", $"/PID {_process.Id} /T")
                  {
                      CreateNoWindow = true, UseShellExecute = false
                  });
                  killer?.WaitForExit(1000);
              }
              catch { /* best effort */ }
          }
          else
          {
              _process.Kill(false); // SIGTERM
          }

          // Wait for graceful exit, then force-kill
          var exited = await WaitForExitAsync(_process, grace);
          if (!exited)
          {
              _process.Kill(true); // force kill entire process tree
          }

          _status.OnNext(ProcessStatus.Stopped);
          CleanupProcess();
      }

      public async Task RestartAsync(ProcessStartInfo psi, TimeSpan? gracePeriod = null)
      {
          await StopAsync(gracePeriod);
          Start(psi);
      }

      private void CleanupProcess()
      {
          _processSubscriptions.Clear();
          _process?.Dispose();
          _process = null;
          StartedAt = null;
      }

      private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
      {
          try { await process.WaitForExitAsync(new CancellationTokenSource(timeout).Token); return true; }
          catch (OperationCanceledException) { return false; }
      }

      public void Dispose()
      {
          _process?.Kill(true);
          _process?.Dispose();
          _processSubscriptions.Dispose();
          _output.Dispose();
          _status.Dispose();
      }
  }
  ```
  **Acceptance**: Can start a long-running process (e.g. `ping localhost -t` on Windows), observe output lines via `Output` subscription, stop it, verify status transitions: Idle → Starting → Running → Stopped.

- [x] **2.6 Process manager (orchestrator)** `[L]`
  **What**: Implement `IProcessManager` — manages the collection of `IProcessRunner` instances. One runner per worktree path. Provides methods to start/stop/restart by worktree path. Exposes aggregate status for tray icon. Uses DynamicData `SourceCache` internally.
  **Files**:
  - `src/Grove.Core/Services/Abstractions/IProcessManager.cs` (new)
  - `src/Grove.Core/Services/ProcessManager.cs` (new)
  **Key code**:
  ```csharp
  public interface IProcessManager : IDisposable
  {
      IObservable<IChangeSet<IProcessRunner, string>> Connect(); // DynamicData
      IProcessRunner GetOrCreate(string worktreePath);
      Task StopAllAsync();
      IObservable<ProcessStatus> AggregateStatus { get; }
  }

  public sealed class ProcessManager : IProcessManager
  {
      private readonly SourceCache<IProcessRunner, string> _runners = new(r => r.WorktreePath);

      public IObservable<IChangeSet<IProcessRunner, string>> Connect() => _runners.Connect();

      public IProcessRunner GetOrCreate(string worktreePath)
      {
          var existing = _runners.Lookup(worktreePath);
          if (existing.HasValue) return existing.Value;

          var runner = new ProcessRunner(worktreePath);
          _runners.AddOrUpdate(runner);
          return runner;
      }

      public IObservable<ProcessStatus> AggregateStatus =>
          _runners.Connect()
              .AutoRefreshOnObservable(r => r.Status)
              .ToCollection()
              .Select(runners =>
              {
                  if (runners.Any(r => r.CurrentStatus == ProcessStatus.Error)) return ProcessStatus.Error;
                  if (runners.Any(r => r.CurrentStatus == ProcessStatus.Running)) return ProcessStatus.Running;
                  return ProcessStatus.Idle;
              });

      public async Task StopAllAsync()
      {
          var tasks = _runners.Items.Select(r => r.StopAsync());
          await Task.WhenAll(tasks);
      }

      public void Dispose()
      {
          foreach (var runner in _runners.Items) runner.Dispose();
          _runners.Dispose();
      }
  }
  ```
  **Acceptance**: Can create runners for multiple worktrees, start processes, observe aggregate status changes.

- [x] **2.7 ANSI parser** `[L]`
  **What**: Implement `AnsiParser` that converts raw console output lines (containing ANSI escape codes) into structured `ConsoleLine` / `ConsoleSpan` objects with foreground/background color and style info. Supports SGR (Select Graphic Rendition) codes for colors (16-color, 256-color, and basic true-color).
  **Files**:
  - `src/Grove.Core/Models/ConsoleLine.cs` (new)
  - `src/Grove.Core/Models/ConsoleSpan.cs` (new)
  - `src/Grove.Core/Models/AnsiColor.cs` (new)
  - `src/Grove.Core/Services/AnsiParser.cs` (new)
  **Key code**:
  ```csharp
  // ConsoleSpan.cs
  public sealed record ConsoleSpan(
      string Text,
      AnsiColor? Foreground = null,
      AnsiColor? Background = null,
      bool IsBold = false,
      bool IsItalic = false,
      bool IsUnderline = false
  );

  // ConsoleLine.cs
  public sealed class ConsoleLine
  {
      public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
      public IReadOnlyList<ConsoleSpan> Spans { get; init; } = [];
      public string RawText { get; init; } = string.Empty;
  }

  // AnsiColor.cs
  public readonly record struct AnsiColor(byte R, byte G, byte B)
  {
      // Standard 16 ANSI colors as static fields
      public static readonly AnsiColor Black = new(0, 0, 0);
      public static readonly AnsiColor Red = new(205, 49, 49);
      public static readonly AnsiColor Green = new(13, 188, 121);
      public static readonly AnsiColor Yellow = new(229, 229, 16);
      public static readonly AnsiColor Blue = new(36, 114, 200);
      public static readonly AnsiColor Magenta = new(188, 63, 188);
      public static readonly AnsiColor Cyan = new(17, 168, 205);
      public static readonly AnsiColor White = new(229, 229, 229);
      // ... bright variants
  }

  // AnsiParser.cs
  public sealed class AnsiParser
  {
      private AnsiColor? _currentFg;
      private AnsiColor? _currentBg;
      private bool _bold, _italic, _underline;

      public ConsoleLine Parse(string rawLine)
      {
          var spans = new List<ConsoleSpan>();
          // Regex to match ESC[...m sequences
          var regex = new Regex(@"\x1B\[([0-9;]*)m");
          int lastIndex = 0;

          foreach (Match match in regex.Matches(rawLine))
          {
              // Add text before this escape sequence
              if (match.Index > lastIndex)
              {
                  var text = rawLine[lastIndex..match.Index];
                  spans.Add(new ConsoleSpan(text, _currentFg, _currentBg, _bold, _italic, _underline));
              }
              // Process SGR codes
              ProcessSgrCodes(match.Groups[1].Value);
              lastIndex = match.Index + match.Length;
          }

          // Remaining text
          if (lastIndex < rawLine.Length)
          {
              spans.Add(new ConsoleSpan(rawLine[lastIndex..], _currentFg, _currentBg, _bold, _italic, _underline));
          }

          return new ConsoleLine { Spans = spans, RawText = rawLine };
      }

      private void ProcessSgrCodes(string codes) { /* parse ; separated codes, update state */ }
  }
  ```
  **Acceptance**: Parsing `"\x1B[31mError:\x1B[0m something failed"` produces two spans: red "Error:" and default "something failed".

- [x] **2.8 Console buffer (DynamicData ring buffer)** `[M]`
  **What**: Implement `ConsoleBuffer` — a DynamicData `SourceList<ConsoleLine>` that acts as a ring buffer (max 10,000 lines). Exposes a `Connect()` for the UI to bind to. Accepts `IObservable<string>` from a `ProcessRunner` and parses each line through `AnsiParser`.
  **Files**:
  - `src/Grove.Core/Services/ConsoleBuffer.cs` (new)
  **Key code**:
  ```csharp
  public sealed class ConsoleBuffer : IDisposable
  {
      private readonly SourceList<ConsoleLine> _lines = new();
      private readonly AnsiParser _parser = new();
      private readonly int _maxLines;
      private IDisposable? _subscription;

      public ConsoleBuffer(int maxLines = 10_000)
      {
          _maxLines = maxLines;
      }

      public IObservable<IChangeSet<ConsoleLine>> Connect() => _lines.Connect();

      public void Attach(IObservable<string> outputStream)
      {
          _subscription?.Dispose();
          _subscription = outputStream.Subscribe(line =>
          {
              var parsed = _parser.Parse(line);
              _lines.Edit(list =>
              {
                  list.Add(parsed);
                  while (list.Count > _maxLines)
                      list.RemoveAt(0);
              });
          });
      }

      public void Clear() => _lines.Clear();

      public void Detach()
      {
          _subscription?.Dispose();
          _subscription = null;
      }

      public void Dispose()
      {
          _subscription?.Dispose();
          _lines.Dispose();
      }
  }
  ```
  **Acceptance**: Attach to a mock `IObservable<string>` that emits 15,000 lines; verify buffer contains exactly 10,000 lines (oldest trimmed). `Connect()` emits change sets.

---

### Phase 3: MVVM ViewModels

- [x] **3.1 MainWindowViewModel — app shell** `[L]`
  **What**: The root ViewModel. Holds the sidebar data (roots + worktrees), the currently selected worktree, and navigation state (detail vs settings). Uses DynamicData `SourceCache` for roots and worktrees. Orchestrates loading config and discovering worktrees on startup.
  **Files**:
  - `src/Grove/ViewModels/MainWindowViewModel.cs` (rewrite from template)
  **Key code**:
  ```csharp
  public class MainWindowViewModel : ReactiveObject, IActivatableViewModel
  {
      public ViewModelActivator Activator { get; } = new();

      private readonly IGitService _git;
      private readonly IConfigService _config;
      private readonly IProcessManager _processManager;
      private readonly IShellService _shell;

      // Sidebar data
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

      // Detail panel — derived from selection
      private readonly ObservableAsPropertyHelper<WorktreeDetailViewModel?> _detail;
      public WorktreeDetailViewModel? Detail => _detail.Value;

      // Navigation
      private bool _isSettingsOpen;
      public bool IsSettingsOpen
      {
          get => _isSettingsOpen;
          set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
      }

      // Commands
      public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
      public ReactiveCommand<Unit, Unit> AddRootCommand { get; }
      public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

      public MainWindowViewModel(IGitService git, IConfigService config,
          IProcessManager processManager, IShellService shell)
      {
          _git = git;
          _config = config;
          _processManager = processManager;
          _shell = shell;

          // Bind roots to sorted list
          _roots.Connect()
              .SortBy(r => r.Name)
              .ObserveOn(RxApp.MainThreadScheduler)
              .Bind(out _rootList)
              .Subscribe();

          // Detail VM derived from selection
          _detail = this.WhenAnyValue(x => x.SelectedWorktree)
              .Select(wt => wt is null ? null :
                  new WorktreeDetailViewModel(wt.Info, _processManager, _shell, _config))
              .ToProperty(this, x => x.Detail);

          // Commands
          RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAllAsync);
          AddRootCommand = ReactiveCommand.CreateFromTask(AddRootAsync);
          OpenSettingsCommand = ReactiveCommand.Create(() => { IsSettingsOpen = !IsSettingsOpen; });

          // Load on activation
          this.WhenActivated(disposables =>
          {
              RefreshCommand.Execute().Subscribe().DisposeWith(disposables);
          });
      }

      private async Task RefreshAllAsync(CancellationToken ct)
      {
          await _config.LoadAsync(ct);
          _roots.Edit(cache =>
          {
              cache.Clear();
              // Populate from config — actual worktree discovery happens in RootViewModel
          });
          foreach (var rootConfig in _config.Config.Roots)
          {
              var rootVm = new RootViewModel(rootConfig, _git);
              _roots.AddOrUpdate(rootVm);
              await rootVm.LoadWorktreesCommand.Execute();
          }
      }
  }
  ```
  **Acceptance**: ViewModel loads config, populates roots, selecting a worktree creates a detail VM.

- [x] **3.2 RootViewModel — repo group in sidebar** `[M]`
  **What**: Represents a single root (repo or scan folder) in the sidebar. Contains a DynamicData collection of `WorktreeViewModel` children. Handles worktree discovery for its root. Supports expand/collapse.
  **Files**:
  - `src/Grove/ViewModels/RootViewModel.cs` (new)
  **Key code**:
  ```csharp
  public class RootViewModel : ReactiveObject
  {
      private readonly RootConfig _rootConfig;
      private readonly IGitService _git;

      public string Id => _rootConfig.Id;
      public string Name => Path.GetFileName(_rootConfig.Path.TrimEnd(Path.DirectorySeparatorChar));
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
      }

      private async Task LoadWorktreesAsync(CancellationToken ct)
      {
          var repoPaths = _rootConfig.Mode == RootMode.Scan
              ? await _git.DiscoverReposAsync(_rootConfig.Path, ct)
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
  ```
  **Acceptance**: RootViewModel discovers worktrees and exposes them as a bound collection.

- [x] **3.3 WorktreeViewModel — sidebar entry** `[M]`
  **What**: Represents a single worktree entry in the sidebar. Shows branch name, short path, and status dot. Status is derived from the process runner's status observable (if a process exists for this worktree).
  **Files**:
  - `src/Grove/ViewModels/WorktreeViewModel.cs` (new)
  **Key code**:
  ```csharp
  public class WorktreeViewModel : ReactiveObject
  {
      public WorktreeInfo Info { get; }

      public string BranchName => Info.BranchName;
      public string ShortPath => ShortenPath(Info.Path);

      private ProcessStatus _status = ProcessStatus.Idle;
      public ProcessStatus Status
      {
          get => _status;
          set => this.RaiseAndSetIfChanged(ref _status, value);
      }

      // OAPH for status indicator color
      private readonly ObservableAsPropertyHelper<string> _statusColor;
      public string StatusColor => _statusColor.Value;

      public WorktreeViewModel(WorktreeInfo info)
      {
          Info = info;

          _statusColor = this.WhenAnyValue(x => x.Status)
              .Select(s => s switch
              {
                  ProcessStatus.Running => "#4EC9B0",  // green
                  ProcessStatus.Error => "#F44747",     // red
                  ProcessStatus.Starting => "#DCDCAA",  // amber
                  _ => "#808080"                         // grey
              })
              .ToProperty(this, x => x.StatusColor);
      }

      public void BindToRunner(IProcessRunner runner)
      {
          runner.Status
              .ObserveOn(RxApp.MainThreadScheduler)
              .Subscribe(s => Status = s);
      }

      private static string ShortenPath(string path) =>
          path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");
  }
  ```
  **Acceptance**: Status changes on the runner are reflected in the ViewModel's Status and StatusColor properties.

- [x] **3.4 WorktreeDetailViewModel — detail panel** `[XL]`
  **What**: The main detail panel ViewModel. Contains header info, command bar state, preset management, and console buffer. Orchestrates process lifecycle (run/stop/restart). This is the most complex ViewModel.
  **Files**:
  - `src/Grove/ViewModels/WorktreeDetailViewModel.cs` (new)
  **Key code**:
  ```csharp
  public class WorktreeDetailViewModel : ReactiveObject, IDisposable
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

      public WorktreeDetailViewModel(WorktreeInfo info, IProcessManager processManager,
          IShellService shell, IConfigService config)
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

          // Console buffer
          _consoleBuffer.Attach(runner.Output);
          _consoleBuffer.Connect()
              .ObserveOn(RxApp.MainThreadScheduler)
              .Bind(out _consoleLines)
              .Subscribe()
              .DisposeWith(_disposables);

          // Presets from config
          var presetsSource = new SourceList<CommandPreset>();
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
              (s, c) => s != ProcessStatus.Running && !string.IsNullOrWhiteSpace(c));
          var canStop = this.WhenAnyValue(x => x.Status,
              s => s == ProcessStatus.Running);

          RunCommand = ReactiveCommand.Create(DoRun, canRun);
          StopCommand = ReactiveCommand.CreateFromTask(DoStopAsync, canStop);
          RestartCommand = ReactiveCommand.CreateFromTask(DoRestartAsync, canStop);
          ClearConsoleCommand = ReactiveCommand.Create(() => _consoleBuffer.Clear());
          CopyConsoleCommand = ReactiveCommand.CreateFromTask(DoCopyConsoleAsync);
          LoadPresetCommand = ReactiveCommand.Create<CommandPreset>(preset => Command = preset.Command);
      }

      private void DoRun()
      {
          var envOverrides = _config.Config.Worktrees
              .GetValueOrDefault(_info.Path)?.Env;
          var psi = _shell.CreateStartInfo(Command, _info.Path, envOverrides);
          var runner = _processManager.GetOrCreate(_info.Path);
          runner.Start(psi);

          // Persist command choice
          SaveWorktreeCommand();
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

      private void SaveWorktreeCommand()
      {
          if (!_config.Config.Worktrees.ContainsKey(_info.Path))
              _config.Config.Worktrees[_info.Path] = new WorktreeConfig();
          _config.Config.Worktrees[_info.Path].Command = Command;
          _ = _config.SaveAsync(); // fire and forget
      }

      private static string? FormatElapsed(DateTimeOffset? startedAt)
      {
          if (startedAt is null) return null;
          var elapsed = DateTimeOffset.Now - startedAt.Value;
          return elapsed.TotalMinutes < 1 ? "just now" : $"{(int)elapsed.TotalMinutes}m ago";
      }

      public void Dispose()
      {
          _disposables.Dispose();
          _consoleBuffer.Dispose();
      }
  }
  ```
  **Acceptance**: Selecting a worktree creates this VM; Run/Stop/Restart commands work with correct CanExecute; console lines stream in; elapsed time updates.

- [x] **3.5 SettingsViewModel** `[L]`
  **What**: ViewModel for the settings page. Manages roots list, presets list, global defaults, per-worktree overrides, and appearance settings. All mutations save config immediately.
  **Files**:
  - `src/Grove/ViewModels/SettingsViewModel.cs` (new)
  **Key code**:
  ```csharp
  public class SettingsViewModel : ReactiveObject
  {
      private readonly IConfigService _config;

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
      public ReactiveCommand<Unit, Unit> SaveCommand { get; }

      public SettingsViewModel(IConfigService config)
      {
          _config = config;
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
  }
  ```
  **Acceptance**: Changing settings persists to config file; adding/removing roots and presets works.

- [x] **3.6 AddWorktreeViewModel — dialog** `[M]`
  **What**: ViewModel for the "add worktree" dialog. Takes a root path, lets user enter a branch name and optional custom path, then calls `git worktree add`.
  **Files**:
  - `src/Grove/ViewModels/AddWorktreeViewModel.cs` (new)
  **Key code**:
  ```csharp
  public class AddWorktreeViewModel : ReactiveObject
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
  ```
  **Acceptance**: Dialog creates a worktree via git CLI; result is returned to caller.

- [x] **3.7 ViewModelBase and shared infrastructure** `[S]`
  **What**: Create a `ViewModelBase` class that all ViewModels inherit from (extends `ReactiveObject`, implements `IActivatableViewModel`). Also create any shared reactive extensions or helpers.
  **Files**:
  - `src/Grove/ViewModels/ViewModelBase.cs` (new)
  **Key code**:
  ```csharp
  public abstract class ViewModelBase : ReactiveObject, IActivatableViewModel
  {
      public ViewModelActivator Activator { get; } = new();
  }
  ```
  Note: The template may already generate this. Verify and update to ensure it extends `ReactiveObject` (not `ObservableObject` from CommunityToolkit).
  **Acceptance**: All ViewModels compile using `ViewModelBase` as base class.

---

### Phase 4: Views & Styling

- [x] **4.1 App theme and global styles** `[L]`
  **What**: Define the Grove visual theme. Use Avalonia's `FluentTheme` as base, then override with custom styles for the dark sidebar, dark console, accent colors, and typography. Support light/dark/system theme switching.
  **Files**:
  - `src/Grove/App.axaml` (modify — add theme resources)
  - `src/Grove/Styles/GroveTheme.axaml` (new)
  - `src/Grove/Styles/Colors.axaml` (new)
  **Key code**:
  ```xml
  <!-- Colors.axaml -->
  <ResourceDictionary xmlns="https://github.com/avaloniaui"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Sidebar -->
    <Color x:Key="SidebarBackground">#1E1E2E</Color>
    <Color x:Key="SidebarForeground">#CDD6F4</Color>
    <Color x:Key="SidebarGroupHeader">#A6ADC8</Color>

    <!-- Console -->
    <Color x:Key="ConsoleBackground">#11111B</Color>
    <Color x:Key="ConsoleForeground">#CDD6F4</Color>

    <!-- Status dots -->
    <Color x:Key="StatusRunning">#4EC9B0</Color>
    <Color x:Key="StatusIdle">#808080</Color>
    <Color x:Key="StatusError">#F44747</Color>
    <Color x:Key="StatusStarting">#DCDCAA</Color>

    <!-- Accent -->
    <Color x:Key="AccentGreen">#4EC9B0</Color>
    <Color x:Key="DetailBackground">#181825</Color>
  </ResourceDictionary>
  ```
  **Acceptance**: App renders with dark theme matching the mockup. Theme can be switched at runtime.

- [x] **4.2 MainWindow — shell layout** `[M]`
  **What**: Implement the main window as a `ReactiveWindow<MainWindowViewModel>`. Two-panel layout: sidebar (fixed width ~280px) + detail panel (fills remaining). Title bar shows "grove" with icon. Conditional display: detail panel vs settings page based on `IsSettingsOpen`.
  **Files**:
  - `src/Grove/Views/MainWindow.axaml` (rewrite)
  - `src/Grove/Views/MainWindow.axaml.cs` (rewrite)
  **Key code**:
  ```xml
  <Window xmlns="https://github.com/avaloniaui"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:vm="using:Grove.ViewModels"
          xmlns:views="using:Grove.Views"
          x:Class="Grove.Views.MainWindow"
          x:DataType="vm:MainWindowViewModel"
          Title="grove" Width="1100" Height="700" MinWidth="800" MinHeight="500">
    <Grid ColumnDefinitions="280,*">
      <!-- Sidebar -->
      <views:SidebarView Grid.Column="0" DataContext="{Binding}" />

      <!-- Detail or Settings -->
      <ContentControl Grid.Column="1">
        <ContentControl.Content>
          <MultiBinding Converter="{x:Static views:DetailOrSettingsConverter.Instance}">
            <Binding Path="IsSettingsOpen" />
            <Binding Path="Detail" />
          </MultiBinding>
        </ContentControl.Content>
      </ContentControl>
    </Grid>
  </Window>
  ```
  ```csharp
  public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
  {
      public MainWindow()
      {
          InitializeComponent();
      }
  }
  ```
  **Acceptance**: Window renders with sidebar on left, detail on right. Resizing works correctly.

- [x] **4.3 SidebarView — repo groups and worktree entries** `[L]`
  **What**: Implement the sidebar as a `ReactiveUserControl`. Shows the Grove logo/title at top, then a scrollable list of root groups. Each root is a collapsible `TreeView` or `Expander` with worktree entries underneath. Bottom has "+ add worktree" and "+ add root" buttons. Worktree entries show status dot + branch name + short path.
  **Files**:
  - `src/Grove/Views/SidebarView.axaml` (new)
  - `src/Grove/Views/SidebarView.axaml.cs` (new)
  - `src/Grove/Views/WorktreeEntryView.axaml` (new — DataTemplate for worktree items)
  - `src/Grove/Views/WorktreeEntryView.axaml.cs` (new)
  **Key code**:
  ```xml
  <!-- SidebarView.axaml -->
  <UserControl xmlns="https://github.com/avaloniaui"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:vm="using:Grove.ViewModels"
               x:Class="Grove.Views.SidebarView"
               x:DataType="vm:MainWindowViewModel">
    <Border Background="{DynamicResource SidebarBackground}">
      <DockPanel>
        <!-- Header -->
        <StackPanel DockPanel.Dock="Top" Margin="16,12">
          <TextBlock Text="🌿 grove" FontSize="18" FontWeight="Bold"
                     Foreground="{DynamicResource SidebarForeground}" />
        </StackPanel>

        <!-- Bottom buttons -->
        <StackPanel DockPanel.Dock="Bottom" Margin="16,8">
          <Button Content="+ add worktree" Command="{Binding AddWorktreeCommand}"
                  Classes="sidebar-action" />
          <Button Content="+ add root" Command="{Binding AddRootCommand}"
                  Classes="sidebar-action" />
        </StackPanel>

        <!-- Root groups -->
        <ScrollViewer>
          <ItemsControl ItemsSource="{Binding Roots}">
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="vm:RootViewModel">
                <StackPanel Margin="0,4">
                  <!-- Root header -->
                  <Button Command="{Binding ToggleExpandCommand}" Classes="root-header">
                    <TextBlock Text="{Binding Name}" FontWeight="SemiBold"
                               Foreground="{DynamicResource SidebarGroupHeader}" />
                  </Button>
                  <!-- Worktree entries (visible when expanded) -->
                  <ItemsControl ItemsSource="{Binding Worktrees}"
                                IsVisible="{Binding IsExpanded}">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate DataType="vm:WorktreeViewModel">
                        <!-- Worktree entry with status dot -->
                        <Button Classes="worktree-entry"
                                Command="{Binding $parent[ItemsControl].((vm:MainWindowViewModel)DataContext).SelectWorktreeCommand}"
                                CommandParameter="{Binding}">
                          <StackPanel Orientation="Horizontal" Spacing="8">
                            <Ellipse Width="8" Height="8"
                                     Fill="{Binding StatusColor}" />
                            <StackPanel>
                              <TextBlock Text="{Binding BranchName}" FontWeight="Medium" />
                              <TextBlock Text="{Binding ShortPath}" FontSize="11" Opacity="0.6" />
                            </StackPanel>
                          </StackPanel>
                        </Button>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </ScrollViewer>
      </DockPanel>
    </Border>
  </UserControl>
  ```
  **Acceptance**: Sidebar shows roots with worktrees; clicking a worktree selects it; status dots render with correct colors.

- [x] **4.4 WorktreeDetailView — header + command bar + console** `[XL]`
  **What**: The main detail panel view. Three zones: header (branch name, path, upstream, status badge), command bar (text input + run/stop/restart buttons + preset chips), and console output area. Uses `ReactiveUserControl<WorktreeDetailViewModel>`.
  **Files**:
  - `src/Grove/Views/WorktreeDetailView.axaml` (new)
  - `src/Grove/Views/WorktreeDetailView.axaml.cs` (new)
  **Key code**:
  ```xml
  <UserControl xmlns="https://github.com/avaloniaui"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:vm="using:Grove.ViewModels"
               x:Class="Grove.Views.WorktreeDetailView"
               x:DataType="vm:WorktreeDetailViewModel">
    <DockPanel Background="{DynamicResource DetailBackground}">
      <!-- Header -->
      <Border DockPanel.Dock="Top" Padding="24,16">
        <Grid ColumnDefinitions="*,Auto">
          <StackPanel>
            <TextBlock Text="{Binding BranchName}" FontSize="24" FontWeight="Bold" />
            <StackPanel Orientation="Horizontal" Spacing="8" Opacity="0.6">
              <TextBlock Text="{Binding FullPath}" />
              <TextBlock Text="·" />
              <TextBlock Text="{Binding UpstreamBranch}" />
            </StackPanel>
          </StackPanel>
          <!-- Status badge -->
          <Border Grid.Column="1" Classes="status-badge"
                  Classes.running="{Binding IsRunning}">
            <TextBlock Text="{Binding StatusText}" />
          </Border>
        </Grid>
      </Border>

      <!-- Command bar -->
      <Border DockPanel.Dock="Top" Padding="24,8">
        <StackPanel Spacing="8">
          <TextBlock Text="COMMAND" FontSize="11" Opacity="0.5" />
          <Grid ColumnDefinitions="*,Auto,Auto">
            <TextBox Text="{Binding Command}" FontFamily="Cascadia Code,Consolas,monospace"
                     Watermark="Enter command..." />
            <Button Grid.Column="1" Content="stop" Command="{Binding StopCommand}" Margin="8,0,0,0" />
            <Button Grid.Column="2" Content="restart" Command="{Binding RestartCommand}" Margin="8,0,0,0" />
          </Grid>
          <!-- Preset chips -->
          <ItemsControl ItemsSource="{Binding Presets}">
            <ItemsControl.ItemsPanel>
              <ItemsPanelTemplate>
                <WrapPanel Orientation="Horizontal" />
              </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <Button Content="{Binding Name}" Classes="preset-chip"
                        Command="{Binding $parent[UserControl].((vm:WorktreeDetailViewModel)DataContext).LoadPresetCommand}"
                        CommandParameter="{Binding}" Margin="0,0,8,0" />
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
      </Border>

      <!-- Console output -->
      <views:ConsoleView DataContext="{Binding}" />
    </DockPanel>
  </UserControl>
  ```
  **Acceptance**: Detail panel shows all three zones; command bar is functional; buttons enable/disable based on process state.

- [x] **4.5 ConsoleView — virtualized ANSI output** `[XL]`
  **What**: The terminal-style console output panel. Uses `ItemsRepeater` for virtualized rendering of `ConsoleLine` items. Each line is rendered as a `TextBlock` with `Inlines` (one `Run` per `ConsoleSpan` with appropriate foreground color). Dark background, monospace font. Shows dim header line "grove · {branch} · started Xm ago". Has a clear button and copy button.
  **Files**:
  - `src/Grove/Views/ConsoleView.axaml` (new)
  - `src/Grove/Views/ConsoleView.axaml.cs` (new)
  - `src/Grove/Converters/ConsoleLineToInlinesConverter.cs` (new)
  **Key code**:
  ```xml
  <!-- ConsoleView.axaml -->
  <UserControl xmlns="https://github.com/avaloniaui"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:vm="using:Grove.ViewModels"
               x:Class="Grove.Views.ConsoleView"
               x:DataType="vm:WorktreeDetailViewModel">
    <Border Background="{DynamicResource ConsoleBackground}" CornerRadius="4" Margin="24,8,24,24">
      <DockPanel>
        <!-- Console header -->
        <Border DockPanel.Dock="Top" Padding="16,8" Opacity="0.4">
          <StackPanel Orientation="Horizontal" Spacing="8">
            <TextBlock Text="grove" FontFamily="Cascadia Code,Consolas,monospace" />
            <TextBlock Text="·" />
            <TextBlock Text="{Binding BranchName}" FontFamily="Cascadia Code,Consolas,monospace" />
            <TextBlock Text="·" />
            <TextBlock Text="{Binding Elapsed, FallbackValue=''}"
                       FontFamily="Cascadia Code,Consolas,monospace" />
          </StackPanel>
        </Border>
        <Separator DockPanel.Dock="Top" Opacity="0.2" />

        <!-- Action buttons -->
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" HorizontalAlignment="Right" Margin="8,4">
          <Button Content="Clear" Command="{Binding ClearConsoleCommand}" Classes="console-action" />
          <Button Content="Copy" Command="{Binding CopyConsoleCommand}" Classes="console-action" />
        </StackPanel>

        <!-- Virtualized console lines -->
        <ScrollViewer x:Name="ConsoleScroller" VerticalScrollBarVisibility="Auto">
          <ItemsRepeater ItemsSource="{Binding ConsoleLines}">
            <ItemsRepeater.ItemTemplate>
              <DataTemplate>
                <!-- Each ConsoleLine rendered with colored spans -->
                <views:ConsoleLineControl Line="{Binding}" />
              </DataTemplate>
            </ItemsRepeater.ItemTemplate>
          </ItemsRepeater>
        </ScrollViewer>
      </DockPanel>
    </Border>
  </UserControl>
  ```
  ```csharp
  // ConsoleView.axaml.cs — auto-scroll behavior
  public partial class ConsoleView : ReactiveUserControl<WorktreeDetailViewModel>
  {
      public ConsoleView()
      {
          InitializeComponent();

          // Auto-scroll to bottom when new lines arrive
          this.WhenActivated(d =>
          {
              this.WhenAnyValue(x => x.ViewModel!.ConsoleLines.Count)
                  .Throttle(TimeSpan.FromMilliseconds(50))
                  .ObserveOn(RxApp.MainThreadScheduler)
                  .Subscribe(_ => ConsoleScroller.ScrollToEnd())
                  .DisposeWith(d);
          });
      }
  }
  ```
  **Acceptance**: Console renders lines with ANSI colors; auto-scrolls; 10,000 lines render without lag (virtualized).

- [x] **4.6 ConsoleLineControl — styled spans** `[M]`
  **What**: Custom control that renders a single `ConsoleLine` as a `TextBlock` with colored `Run` inlines. Each `ConsoleSpan` becomes a `Run` with the appropriate foreground/background brush.
  **Files**:
  - `src/Grove/Views/ConsoleLineControl.cs` (new — code-only control, no AXAML)
  **Key code**:
  ```csharp
  public class ConsoleLineControl : Control
  {
      public static readonly StyledProperty<ConsoleLine?> LineProperty =
          AvaloniaProperty.Register<ConsoleLineControl, ConsoleLine?>(nameof(Line));

      public ConsoleLine? Line
      {
          get => GetValue(LineProperty);
          set => SetValue(LineProperty, value);
      }

      static ConsoleLineControl()
      {
          AffectsRender<ConsoleLineControl>(LineProperty);
      }

      public override void Render(DrawingContext context)
      {
          // Render each span with appropriate color using FormattedText
          // This is more performant than TextBlock with Inlines for large line counts
          if (Line is null) return;

          double x = 4; // left padding
          var typeface = new Typeface("Cascadia Code,Consolas,monospace");
          var fontSize = 13.0; // TODO: bind to settings

          foreach (var span in Line.Spans)
          {
              var brush = span.Foreground is { } fg
                  ? new SolidColorBrush(Color.FromRgb(fg.R, fg.G, fg.B))
                  : Brushes.White;

              var formattedText = new FormattedText(
                  span.Text, CultureInfo.CurrentCulture,
                  FlowDirection.LeftToRight, typeface, fontSize, brush);

              context.DrawText(formattedText, new Point(x, 0));
              x += formattedText.Width;
          }
      }

      protected override Size MeasureOverride(Size availableSize)
      {
          return new Size(availableSize.Width, 20); // fixed line height
      }
  }
  ```
  **Acceptance**: ANSI-colored text renders correctly; performance is good with thousands of lines.

- [x] **4.7 Status dot and badge styles** `[S]`
  **What**: Define reusable styles for the status indicator dot (sidebar) and the status badge (detail header). Dot colors: green (running), grey (idle), red (error), amber (starting). Badge has a border with matching color.
  **Files**:
  - `src/Grove/Styles/StatusStyles.axaml` (new)
  **Key code**:
  ```xml
  <Styles xmlns="https://github.com/avaloniaui"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Status badge -->
    <Style Selector="Border.status-badge">
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="CornerRadius" Value="12" />
      <Setter Property="Padding" Value="12,4" />
      <Setter Property="BorderBrush" Value="{DynamicResource StatusIdle}" />
    </Style>
    <Style Selector="Border.status-badge.running">
      <Setter Property="BorderBrush" Value="{DynamicResource StatusRunning}" />
      <Setter Property="Background" Value="#1A4EC9B0" />
    </Style>

    <!-- Preset chip -->
    <Style Selector="Button.preset-chip">
      <Setter Property="Background" Value="Transparent" />
      <Setter Property="BorderBrush" Value="#404040" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="CornerRadius" Value="4" />
      <Setter Property="Padding" Value="12,4" />
      <Setter Property="FontSize" Value="12" />
      <Setter Property="FontFamily" Value="Cascadia Code,Consolas,monospace" />
    </Style>
  </Styles>
  ```
  **Acceptance**: Status dots and badges render with correct colors matching the mockup.

- [x] **4.8 SettingsView** `[L]`
  **What**: Settings page view with sections for: roots manager (list + add/remove), presets manager (list + add/edit/remove), global defaults (default command, auto-start toggle), per-worktree overrides table, and appearance (theme picker, font size slider).
  **Files**:
  - `src/Grove/Views/SettingsView.axaml` (new)
  - `src/Grove/Views/SettingsView.axaml.cs` (new)
  **Key code**:
  ```xml
  <UserControl xmlns="https://github.com/avaloniaui"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:vm="using:Grove.ViewModels"
               x:Class="Grove.Views.SettingsView"
               x:DataType="vm:SettingsViewModel">
    <ScrollViewer Padding="32">
      <StackPanel Spacing="24" MaxWidth="700">
        <TextBlock Text="Settings" FontSize="24" FontWeight="Bold" />

        <!-- Roots Manager -->
        <HeaderedContentControl Header="Roots">
          <StackPanel Spacing="8">
            <ItemsControl ItemsSource="{Binding Roots}">
              <ItemsControl.ItemTemplate>
                <DataTemplate>
                  <Grid ColumnDefinitions="*,Auto,Auto">
                    <TextBlock Text="{Binding Path}" VerticalAlignment="Center" />
                    <TextBlock Grid.Column="1" Text="{Binding Mode}" Opacity="0.5" Margin="8,0" />
                    <Button Grid.Column="2" Content="✕"
                            Command="{Binding $parent[UserControl].((vm:SettingsViewModel)DataContext).RemoveRootCommand}"
                            CommandParameter="{Binding}" />
                  </Grid>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
            <Button Content="+ Add Root" Command="{Binding AddRootCommand}" />
          </StackPanel>
        </HeaderedContentControl>

        <!-- Global Defaults -->
        <HeaderedContentControl Header="Defaults">
          <StackPanel Spacing="8">
            <TextBox Text="{Binding DefaultCommand}" Watermark="Default command" />
            <CheckBox Content="Auto-start on worktree selection" IsChecked="{Binding AutoStart}" />
          </StackPanel>
        </HeaderedContentControl>

        <!-- Appearance -->
        <HeaderedContentControl Header="Appearance">
          <StackPanel Spacing="8">
            <ComboBox SelectedItem="{Binding Theme}" ItemsSource="{x:Static vm:SettingsViewModel.Themes}" />
            <StackPanel Orientation="Horizontal" Spacing="8">
              <TextBlock Text="Console font size:" VerticalAlignment="Center" />
              <Slider Value="{Binding ConsoleFontSize}" Minimum="10" Maximum="20" Width="200" />
              <TextBlock Text="{Binding ConsoleFontSize}" VerticalAlignment="Center" />
            </StackPanel>
          </StackPanel>
        </HeaderedContentControl>
      </StackPanel>
    </ScrollViewer>
  </UserControl>
  ```
  **Acceptance**: Settings page renders all sections; changes persist to config file.

- [x] **4.9 AddWorktreeDialog** `[M]`
  **What**: Modal dialog for creating a new worktree. Fields: branch name (required), custom path (optional). Create and Cancel buttons.
  **Files**:
  - `src/Grove/Views/AddWorktreeDialog.axaml` (new)
  - `src/Grove/Views/AddWorktreeDialog.axaml.cs` (new)
  **Key code**:
  ```csharp
  public partial class AddWorktreeDialog : ReactiveWindow<AddWorktreeViewModel>
  {
      public AddWorktreeDialog()
      {
          InitializeComponent();

          this.WhenActivated(d =>
          {
              ViewModel!.CreateCommand
                  .Subscribe(result => Close(result))
                  .DisposeWith(d);
              ViewModel!.CancelCommand
                  .Subscribe(_ => Close(null))
                  .DisposeWith(d);
          });
      }
  }
  ```
  **Acceptance**: Dialog opens modally; creating a worktree returns the result; cancel closes without action.

- [x] **4.10 Converters and value converters** `[S]`
  **What**: Implement Avalonia value converters needed across views: `ProcessStatusToColorConverter`, `ProcessStatusToBoolConverter` (for button visibility), `BoolToVisibilityConverter`, etc.
  **Files**:
  - `src/Grove/Converters/ProcessStatusToColorConverter.cs` (new)
  - `src/Grove/Converters/BoolToVisibilityConverter.cs` (new)
  **Key code**:
  ```csharp
  public class ProcessStatusToColorConverter : IValueConverter
  {
      public static readonly ProcessStatusToColorConverter Instance = new();

      public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
      {
          return value is ProcessStatus status ? status switch
          {
              ProcessStatus.Running => new SolidColorBrush(Color.Parse("#4EC9B0")),
              ProcessStatus.Error => new SolidColorBrush(Color.Parse("#F44747")),
              ProcessStatus.Starting => new SolidColorBrush(Color.Parse("#DCDCAA")),
              _ => new SolidColorBrush(Color.Parse("#808080")),
          } : new SolidColorBrush(Colors.Gray);
      }

      public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
          => throw new NotSupportedException();
  }
  ```
  **Acceptance**: Converters work in XAML bindings; status colors render correctly.

- [x] **4.11 ViewLocator setup** `[S]`
  **What**: Configure the `ViewLocator` so ReactiveUI can automatically resolve Views for ViewModels. The template may include one — verify it works with our ViewModel naming convention (`FooViewModel` → `FooView`).
  **Files**:
  - `src/Grove/ViewLocator.cs` (modify or verify from template)
  **Key code**:
  ```csharp
  public class ViewLocator : IViewLocator
  {
      public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
      {
          if (viewModel is null) return null;

          var vmType = viewModel.GetType();
          var viewTypeName = vmType.FullName!.Replace("ViewModel", "View");
          var viewType = Type.GetType(viewTypeName);

          if (viewType is null) return null;

          return (IViewFor)Activator.CreateInstance(viewType)!;
      }
  }
  ```
  **Acceptance**: Setting a ViewModel as DataContext automatically resolves and displays the correct View.

---

### Phase 5: Process & Console Integration

- [x] **5.1 Wire process runner to detail view** `[M]`
  **What**: Connect the `ProcessRunner` output observable to the `ConsoleBuffer` in `WorktreeDetailViewModel`. Ensure output streams to the console view in real-time. Verify thread marshalling is correct (output arrives on background thread, must observe on UI thread for binding).
  **Files**:
  - `src/Grove/ViewModels/WorktreeDetailViewModel.cs` (modify — verify wiring)
  **Key code**:
  ```csharp
  // Already in constructor from 3.4, but verify the full pipeline:
  // ProcessRunner.Output (background thread)
  //   → ConsoleBuffer.Attach() (parses ANSI, adds to SourceList)
  //   → ConsoleBuffer.Connect() (DynamicData change sets)
  //   → .ObserveOn(RxApp.MainThreadScheduler) (marshal to UI)
  //   → .Bind(out _consoleLines) (ReadOnlyObservableCollection)
  //   → ItemsRepeater in ConsoleView (renders)
  ```
  **Acceptance**: Running `echo hello` shows "hello" in the console view. Running a colored command (e.g. `npm test` with colors) shows colored output.

- [x] **5.2 Process lifecycle management** `[M]`
  **What**: Ensure the full process lifecycle works end-to-end: start → running (status green) → stop (graceful then force) → idle. Restart = stop + start. Verify status transitions update sidebar dots and detail badge simultaneously.
  **Files**:
  - `src/Grove/ViewModels/WorktreeViewModel.cs` (modify — bind to runner status)
  - `src/Grove/ViewModels/MainWindowViewModel.cs` (modify — wire sidebar status)
  **Key code**:
  ```csharp
  // In MainWindowViewModel, when creating WorktreeViewModels, bind them to runners:
  // After worktree discovery, check if a runner exists and bind status
  private void BindWorktreeStatuses()
  {
      _processManager.Connect()
          .Subscribe(changeSet =>
          {
              foreach (var change in changeSet)
              {
                  var wt = _rootList.SelectMany(r => r.Worktrees)
                      .FirstOrDefault(w => w.Info.Path == change.Current.WorktreePath);
                  wt?.BindToRunner(change.Current);
              }
          });
  }
  ```
  **Acceptance**: Starting a process turns sidebar dot green and badge to "running"; stopping turns it grey/"idle"; error exit turns it red/"error".

- [x] **5.3 Console auto-scroll and copy** `[S]`
  **What**: Implement auto-scroll behavior (scroll to bottom on new output, unless user has scrolled up). Implement copy-to-clipboard for full console content.
  **Files**:
  - `src/Grove/Views/ConsoleView.axaml.cs` (modify)
  **Key code**:
  ```csharp
  // Auto-scroll with user-scroll detection
  this.WhenActivated(d =>
  {
      var isAtBottom = true;
      ConsoleScroller.ScrollChanged += (_, e) =>
      {
          isAtBottom = ConsoleScroller.Offset.Y >=
              ConsoleScroller.Extent.Height - ConsoleScroller.Viewport.Height - 20;
      };

      this.WhenAnyValue(x => x.ViewModel!.ConsoleLines.Count)
          .Where(_ => isAtBottom)
          .Throttle(TimeSpan.FromMilliseconds(50))
          .ObserveOn(RxApp.MainThreadScheduler)
          .Subscribe(_ => ConsoleScroller.ScrollToEnd())
          .DisposeWith(d);
  });
  ```
  **Acceptance**: Console auto-scrolls; scrolling up pauses auto-scroll; scrolling to bottom resumes it. Copy button copies all raw text to clipboard.

- [x] **5.4 Command persistence and preset loading** `[S]`
  **What**: When a command is run, persist it to config for that worktree. When a preset chip is clicked, load the command into the text box. Ensure the command bar shows the last-used command when re-selecting a worktree.
  **Files**:
  - `src/Grove/ViewModels/WorktreeDetailViewModel.cs` (verify — already in 3.4)
  **Key code**: Already implemented in 3.4's `SaveWorktreeCommand()` and `LoadPresetCommand`.
  **Acceptance**: Run a command, close and reopen the worktree detail — command is preserved. Click a preset chip — command text updates.

- [x] **5.5 Error handling and process exit codes** `[M]`
  **What**: Handle process errors gracefully. Non-zero exit code → error state with red indicator. Display exit code in console. Handle process spawn failures (e.g. command not found) with user-friendly error message in console.
  **Files**:
  - `src/Grove.Core/Services/ProcessRunner.cs` (modify)
  - `src/Grove/ViewModels/WorktreeDetailViewModel.cs` (modify)
  **Key code**:
  ```csharp
  // In ProcessRunner, on exit:
  exited.Subscribe(_ =>
  {
      var exitCode = process.ExitCode;
      _output.OnNext($"\n[grove] Process exited with code {exitCode}");
      _status.OnNext(exitCode == 0 ? ProcessStatus.Idle : ProcessStatus.Error);
      CleanupProcess();
  });

  // In DoRun, wrap in try-catch:
  private void DoRun()
  {
      try
      {
          var psi = _shell.CreateStartInfo(Command, _info.Path, envOverrides);
          var runner = _processManager.GetOrCreate(_info.Path);
          runner.Start(psi);
          SaveWorktreeCommand();
      }
      catch (Exception ex)
      {
          // Push error to console buffer directly
          _consoleBuffer.AddLine($"[grove] Failed to start process: {ex.Message}");
      }
  }
  ```
  **Acceptance**: Running an invalid command shows error in console; exit code is displayed; status dot turns red on non-zero exit.

- [x] **5.6 Environment variable merging** `[S]`
  **What**: Ensure per-worktree environment variable overrides are correctly merged when spawning processes. System env vars are inherited; overrides take precedence.
  **Files**:
  - `src/Grove.Core/Services/ShellService.cs` (verify — already in 2.3)
  **Key code**: Already implemented in `ShellService.CreateStartInfo()` — the `ProcessStartInfo.Environment` dictionary inherits system env vars by default; we just add overrides on top.
  **Acceptance**: Set `PORT=3001` as env override for a worktree; run `echo %PORT%` (Windows) or `echo $PORT` (Unix) — output shows "3001".

---

### Phase 6: System Tray & Platform

- [x] **6.1 System tray icon** `[L]`
  **What**: Implement system tray (notification area) icon using Avalonia's `TrayIcon` API. Tray icon shows aggregate status: green if any process running and none errored, red if any errored, grey if all idle. Clicking the tray icon shows/hides the main window.
  **Files**:
  - `src/Grove/App.axaml` (modify — add TrayIcon)
  - `src/Grove/App.axaml.cs` (modify — tray icon logic)
  - `src/Grove/Assets/tray-idle.ico` (new)
  - `src/Grove/Assets/tray-running.ico` (new)
  - `src/Grove/Assets/tray-error.ico` (new)
  **Key code**:
  ```xml
  <!-- App.axaml -->
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="/Styles/GroveTheme.axaml" />
  </Application.Styles>

  <TrayIcon.Icons>
    <TrayIcons>
      <TrayIcon Icon="/Assets/tray-idle.ico"
                ToolTipText="Grove"
                Command="{Binding ToggleWindowCommand}">
        <TrayIcon.Menu>
          <NativeMenu>
            <NativeMenuItem Header="Show Grove" Command="{Binding ShowWindowCommand}" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Quit" Command="{Binding QuitCommand}" />
          </NativeMenu>
        </TrayIcon.Menu>
      </TrayIcon>
    </TrayIcons>
  </TrayIcon.Icons>
  ```
  ```csharp
  // In App.axaml.cs — subscribe to aggregate status to swap tray icon
  _processManager.AggregateStatus
      .ObserveOn(RxApp.MainThreadScheduler)
      .Subscribe(status =>
      {
          var icon = status switch
          {
              ProcessStatus.Running => new WindowIcon(AssetLoader.Open(new Uri("avares://Grove/Assets/tray-running.ico"))),
              ProcessStatus.Error => new WindowIcon(AssetLoader.Open(new Uri("avares://Grove/Assets/tray-error.ico"))),
              _ => new WindowIcon(AssetLoader.Open(new Uri("avares://Grove/Assets/tray-idle.ico"))),
          };
          // Update tray icon
      });
  ```
  **Acceptance**: Tray icon appears; changes color based on aggregate process status; clicking shows/hides window.

- [x] **6.2 Minimize to tray on close** `[M]`
  **What**: Override the window close behavior: instead of exiting, hide the window and keep running in the background. Processes continue running. Only "Quit" from tray menu actually exits.
  **Files**:
  - `src/Grove/Views/MainWindow.axaml.cs` (modify)
  - `src/Grove/App.axaml.cs` (modify)
  **Key code**:
  ```csharp
  // MainWindow.axaml.cs
  protected override void OnClosing(WindowClosingEventArgs e)
  {
      // Don't actually close — hide to tray
      if (!_isQuitting)
      {
          e.Cancel = true;
          Hide();
      }
      base.OnClosing(e);
  }

  // App.axaml.cs — Quit command
  public ReactiveCommand<Unit, Unit> QuitCommand { get; }

  // In initialization:
  QuitCommand = ReactiveCommand.CreateFromTask(async () =>
  {
      await _processManager.StopAllAsync();
      _isQuitting = true;
      (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
  });
  ```
  **Acceptance**: Closing the window hides it (processes keep running); "Quit" from tray stops all processes and exits.

- [x] **6.3 Tray context menu with running processes** `[M]`
  **What**: The tray context menu shows a list of currently running processes (worktree branch names), plus "Show Grove" and "Quit" items. The list updates dynamically as processes start/stop.
  **Files**:
  - `src/Grove/App.axaml.cs` (modify)
  - `src/Grove/Services/TrayMenuService.cs` (new)
  **Key code**:
  ```csharp
  public sealed class TrayMenuService : IDisposable
  {
      private readonly IProcessManager _processManager;
      private readonly NativeMenu _menu;
      private readonly IDisposable _subscription;

      public TrayMenuService(IProcessManager processManager, NativeMenu menu)
      {
          _processManager = processManager;
          _menu = menu;

          _subscription = _processManager.Connect()
              .AutoRefreshOnObservable(r => r.Status)
              .Filter(r => r.CurrentStatus == ProcessStatus.Running)
              .ToCollection()
              .ObserveOn(RxApp.MainThreadScheduler)
              .Subscribe(runners => RebuildMenu(runners));
      }

      private void RebuildMenu(IReadOnlyCollection<IProcessRunner> runners)
      {
          _menu.Items.Clear();
          foreach (var runner in runners)
          {
              _menu.Items.Add(new NativeMenuItem(
                  $"● {Path.GetFileName(runner.WorktreePath)}"));
          }
          if (runners.Any())
              _menu.Items.Add(new NativeMenuItemSeparator());
          _menu.Items.Add(new NativeMenuItem("Show Grove") { /* command */ });
          _menu.Items.Add(new NativeMenuItemSeparator());
          _menu.Items.Add(new NativeMenuItem("Quit") { /* command */ });
      }
  }
  ```
  **Acceptance**: Tray menu shows running processes; list updates when processes start/stop.

- [x] **6.4 Platform-aware process termination** `[M]`
  **What**: Ensure process termination works correctly on both Windows and Unix. Windows: use `taskkill /PID {pid} /T` for graceful, then `taskkill /PID {pid} /T /F` for force. Unix: SIGTERM then SIGKILL. Handle process tree killing (child processes).
  **Files**:
  - `src/Grove.Core/Services/ProcessRunner.cs` (modify — refine StopAsync)
  **Key code**:
  ```csharp
  public async Task StopAsync(TimeSpan? gracePeriod = null)
  {
      if (_process is null || _process.HasExited) return;
      var grace = gracePeriod ?? TimeSpan.FromSeconds(5);

      _status.OnNext(ProcessStatus.Stopped);

      if (OperatingSystem.IsWindows())
      {
          // Graceful: taskkill without /F sends WM_CLOSE / CTRL_C
          await RunTaskkillAsync(_process.Id, force: false);
          var exited = await WaitForExitAsync(_process, grace);
          if (!exited)
          {
              // Force: taskkill with /F and /T (tree kill)
              await RunTaskkillAsync(_process.Id, force: true);
          }
      }
      else
      {
          _process.Kill(false); // SIGTERM
          var exited = await WaitForExitAsync(_process, grace);
          if (!exited)
              _process.Kill(true); // SIGKILL + children
      }

      CleanupProcess();
  }

  private static async Task RunTaskkillAsync(int pid, bool force)
  {
      var args = force ? $"/PID {pid} /T /F" : $"/PID {pid} /T";
      using var p = Process.Start(new ProcessStartInfo("taskkill", args)
      {
          CreateNoWindow = true, UseShellExecute = false,
          RedirectStandardOutput = true, RedirectStandardError = true
      });
      if (p is not null) await p.WaitForExitAsync();
  }
  ```
  **Acceptance**: Stopping a process on Windows kills the entire process tree. On Unix, SIGTERM is sent first, then SIGKILL after timeout.

- [x] **6.5 Folder picker integration** `[S]`
  **What**: Implement folder picker for "Add Root" functionality using Avalonia's `StorageProvider` API (the modern replacement for `OpenFolderDialog`).
  **Files**:
  - `src/Grove/ViewModels/SettingsViewModel.cs` (modify — AddRootAsync)
  - `src/Grove/Services/IDialogService.cs` (new)
  - `src/Grove/Services/DialogService.cs` (new)
  **Key code**:
  ```csharp
  public interface IDialogService
  {
      Task<string?> PickFolderAsync(string title);
  }

  public class DialogService : IDialogService
  {
      private readonly Window _owner;

      public DialogService(Window owner) => _owner = owner;

      public async Task<string?> PickFolderAsync(string title)
      {
          var result = await _owner.StorageProvider.OpenFolderPickerAsync(
              new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
          return result.FirstOrDefault()?.Path.LocalPath;
      }
  }
  ```
  **Acceptance**: Clicking "Add Root" opens a native folder picker; selected path is added to config.

- [x] **6.6 Theme switching at runtime** `[M]`
  **What**: Implement runtime theme switching between Light, Dark, and System. Use Avalonia's `RequestedThemeVariant` on the `Application` object. Persist the choice to config.
  **Files**:
  - `src/Grove/App.axaml.cs` (modify)
  - `src/Grove/ViewModels/SettingsViewModel.cs` (modify — theme change handler)
  **Key code**:
  ```csharp
  // In App.axaml.cs or a ThemeService
  public void ApplyTheme(AppTheme theme)
  {
      RequestedThemeVariant = theme switch
      {
          AppTheme.Light => ThemeVariant.Light,
          AppTheme.Dark => ThemeVariant.Dark,
          AppTheme.System => ThemeVariant.Default,
          _ => ThemeVariant.Dark
      };
  }

  // In SettingsViewModel, react to theme changes:
  this.WhenAnyValue(x => x.Theme)
      .Skip(1)
      .Subscribe(theme => _themeService.ApplyTheme(theme));
  ```
  **Acceptance**: Changing theme in settings immediately updates the UI; choice persists across restarts.

- [x] **6.7 Add worktree via git CLI** `[M]`
  **What**: Implement the `AddWorktreeAsync` method in `GitService`. Runs `git worktree add <path> <branch>`. Handles errors (branch already exists, path conflict). After creation, refreshes the worktree list.
  **Files**:
  - `src/Grove.Core/Services/GitService.cs` (modify — implement AddWorktreeAsync)
  **Key code**:
  ```csharp
  public async Task<WorktreeInfo?> AddWorktreeAsync(
      string repoPath, string branchName, string? path = null, CancellationToken ct = default)
  {
      var targetPath = path ?? Path.Combine(
          Path.GetDirectoryName(repoPath)!,
          $"{Path.GetFileName(repoPath)}-{branchName.Replace("/", "-")}");

      var psi = new ProcessStartInfo("git", $"worktree add \"{targetPath}\" {branchName}")
      {
          WorkingDirectory = repoPath,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true,
      };

      using var proc = Process.Start(psi)!;
      var stderr = await proc.StandardError.ReadToEndAsync(ct);
      await proc.WaitForExitAsync(ct);

      if (proc.ExitCode != 0)
          throw new InvalidOperationException($"git worktree add failed: {stderr}");

      // Return the newly created worktree info
      var worktrees = await GetWorktreesAsync(repoPath, ct);
      return worktrees.FirstOrDefault(w => w.Path == targetPath);
  }
  ```
  **Acceptance**: "Add worktree" dialog creates a new worktree; it appears in the sidebar after refresh.

- [x] **6.8 Upstream branch detection** `[S]`
  **What**: Implement `GetUpstreamBranchAsync` in `GitService` to get the upstream tracking branch for a worktree (e.g. `origin/main`). Displayed in the detail header.
  **Files**:
  - `src/Grove.Core/Services/GitService.cs` (modify)
  **Key code**:
  ```csharp
  public async Task<string?> GetUpstreamBranchAsync(string worktreePath, CancellationToken ct = default)
  {
      var psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref @{upstream}")
      {
          WorkingDirectory = worktreePath,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true,
      };

      using var proc = Process.Start(psi)!;
      var output = await proc.StandardOutput.ReadToEndAsync(ct);
      await proc.WaitForExitAsync(ct);

      return proc.ExitCode == 0 ? output.Trim() : null;
  }
  ```
  **Acceptance**: Detail header shows "origin/main" (or similar) for worktrees with an upstream.

- [x] **6.9 App icon and assets** `[S]`
  **What**: Create app icon (tree/grove themed), tray icons (colored variants for idle/running/error), and any other visual assets. Set the app icon in the project file and window.
  **Files**:
  - `src/Grove/Assets/grove-icon.ico` (new)
  - `src/Grove/Assets/grove-icon.png` (new)
  - `src/Grove/Assets/tray-idle.ico` (new)
  - `src/Grove/Assets/tray-running.ico` (new)
  - `src/Grove/Assets/tray-error.ico` (new)
  - `src/Grove/Grove.csproj` (modify — set ApplicationIcon)
  **Key code**:
  ```xml
  <!-- Grove.csproj -->
  <PropertyGroup>
    <ApplicationIcon>Assets/grove-icon.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>
  ```
  **Acceptance**: App window and taskbar show the Grove icon; tray icon renders correctly.

- [x] **6.10 First-launch experience** `[M]`
  **What**: On first launch (no config file exists), show a welcome state: empty sidebar with prominent "Add your first root" button/message. Guide the user to add a repo root via folder picker. After adding, auto-discover worktrees and populate the sidebar.
  **Files**:
  - `src/Grove/Views/EmptyStateView.axaml` (new)
  - `src/Grove/Views/EmptyStateView.axaml.cs` (new)
  - `src/Grove/ViewModels/MainWindowViewModel.cs` (modify — handle empty state)
  **Key code**:
  ```csharp
  // In MainWindowViewModel
  private readonly ObservableAsPropertyHelper<bool> _hasRoots;
  public bool HasRoots => _hasRoots.Value;

  // In constructor:
  _hasRoots = _roots.CountChanged
      .Select(count => count > 0)
      .ToProperty(this, x => x.HasRoots);
  ```
  ```xml
  <!-- In MainWindow.axaml, show empty state when no roots -->
  <Panel Grid.Column="1" IsVisible="{Binding !HasRoots}">
    <views:EmptyStateView />
  </Panel>
  ```
  **Acceptance**: First launch shows empty state with "Add root" prompt; after adding a root, sidebar populates.

---

## Verification

- [x] `dotnet build Grove.sln` succeeds with zero warnings
- [x] App launches on Windows with .NET 10
- [x] Adding a repo root discovers worktrees and displays them in sidebar
- [x] Selecting a worktree shows detail panel with correct branch/path/upstream
- [x] Running a command starts the process; console shows live output
- [x] ANSI colors render correctly in console (test with `npm test` or colored output)
- [x] Stop button terminates the process; status changes to idle
- [x] Restart button stops then starts the process
- [x] Status dots in sidebar update in real-time (green/grey/red)
- [x] Preset chips load commands into the command bar
- [x] Closing the window minimizes to system tray
- [x] Tray icon color reflects aggregate process status
- [x] Tray context menu shows running processes
- [x] "Quit" from tray stops all processes and exits the app
- [x] Settings page: add/remove roots works
- [x] Settings page: add/edit/remove presets works
- [x] Settings page: theme switching works (light/dark/system)
- [x] Config persists across app restarts
- [x] Console ring buffer caps at 10,000 lines (no memory leak)
- [x] Console auto-scrolls; pauses when user scrolls up
- [x] No CommunityToolkit.Mvvm references anywhere in the codebase
- [x] All ViewModels extend `ReactiveObject` (not `ObservableObject`)
- [x] All commands are `ReactiveCommand` (not `RelayCommand`)

---

## Dependency Graph

```
Phase 1 (Scaffolding)
  └── 1.1 Solution + App ──┐
  └── 1.2 Core Library ────┤
  └── 1.3 NuGet Packages ──┤
  └── 1.6 Build Props ─────┤
  └── 1.7 .gitignore ──────┤
      ├───────────────────> 1.4 DI Container ──> 1.5 Folder Structure
      │
Phase 2 (Core Services) ── depends on Phase 1
  └── 2.1 Models ──────────┐
      ├──────────────────> 2.2 Config Service
      ├──────────────────> 2.3 Shell Service
      ├──────────────────> 2.4 Git Service (depends on 2.3)
      ├──────────────────> 2.7 ANSI Parser
      │
      ├── 2.5 Process Runner (depends on 2.3)
      │     └──> 2.6 Process Manager (depends on 2.5)
      │
      └── 2.8 Console Buffer (depends on 2.7)

Phase 3 (ViewModels) ───── depends on Phase 2
  └── 3.7 ViewModelBase ───┐
      ├──────────────────> 3.3 WorktreeViewModel
      ├──────────────────> 3.2 RootViewModel (depends on 3.3)
      ├──────────────────> 3.4 WorktreeDetailViewModel (depends on 2.5, 2.6, 2.8)
      ├──────────────────> 3.5 SettingsViewModel
      ├──────────────────> 3.6 AddWorktreeViewModel
      └──────────────────> 3.1 MainWindowViewModel (depends on 3.2, 3.3, 3.4, 3.5)

Phase 4 (Views) ────────── depends on Phase 3
  └── 4.1 Theme + Styles ──┐
  └── 4.7 Status Styles ───┤
  └── 4.10 Converters ─────┤
  └── 4.11 ViewLocator ────┤
      ├──────────────────> 4.2 MainWindow
      ├──────────────────> 4.3 SidebarView
      ├──────────────────> 4.6 ConsoleLineControl
      │                      └──> 4.5 ConsoleView (depends on 4.6)
      │                             └──> 4.4 WorktreeDetailView (depends on 4.5)
      ├──────────────────> 4.8 SettingsView
      └──────────────────> 4.9 AddWorktreeDialog

Phase 5 (Integration) ──── depends on Phase 3 + 4
  └── 5.1 Wire process to console
  └── 5.2 Process lifecycle (sidebar status)
  └── 5.3 Console auto-scroll + copy
  └── 5.4 Command persistence
  └── 5.5 Error handling
  └── 5.6 Env var merging

Phase 6 (Platform) ─────── depends on Phase 5
  └── 6.1 System tray icon
  └── 6.2 Minimize to tray
  └── 6.3 Tray context menu (depends on 6.1)
  └── 6.4 Platform process termination
  └── 6.5 Folder picker
  └── 6.6 Theme switching
  └── 6.7 Add worktree (git CLI)
  └── 6.8 Upstream branch detection
  └── 6.9 App icon + assets
  └── 6.10 First-launch experience
```

---

## Risk Register

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|------------|------------|
| 1 | **Avalonia 11.2 doesn't support net10.0 TFM** | High | Medium | Fall back to `net9.0` if needed; Avalonia 11.2 targets `netstandard2.0` so it should work. Test early in Phase 1. |
| 2 | **Process tree killing on Windows is unreliable** | Medium | Medium | Use `taskkill /T /F` as fallback; consider using Job Objects via P/Invoke if taskkill is insufficient. |
| 3 | **ANSI parser doesn't handle all escape sequences** | Low | High | Start with SGR (colors/styles) only; ignore cursor movement, screen clearing, etc. Log unhandled sequences for future improvement. |
| 4 | **ItemsRepeater performance with 10,000 console lines** | Medium | Medium | Use virtualization (ItemsRepeater handles this). If still slow, switch to a custom `DrawingContext`-based renderer that draws visible lines only. |
| 5 | **System tray API differences across platforms** | Medium | Low | Avalonia's `TrayIcon` abstracts this. Test on Windows first (primary target). |
| 6 | **DynamicData thread safety with process output** | High | Medium | Always use `.ObserveOn(RxApp.MainThreadScheduler)` before `.Bind()`. Use `SourceList.Edit()` for batch mutations. |
| 7 | **ReactiveUI ViewLocator conflicts with DI** | Low | Medium | Use a custom `ViewLocator` that resolves from the DI container, or keep the convention-based one and register views manually if needed. |
| 8 | **Config file corruption on concurrent writes** | Low | Low | Use a write lock (SemaphoreSlim) in `ConfigService.SaveAsync()`. Throttle saves with Rx. |
| 9 | **Scan mode discovers too many repos (deep directory trees)** | Medium | Medium | Add a max depth limit (default 3). Show progress indicator during scan. Allow cancellation. |
| 10 | **Template generates CommunityToolkit code** | Low | Low | The `-m ReactiveUI` flag should generate ReactiveUI code. Verify immediately in Phase 1 and remove any CommunityToolkit references. |
