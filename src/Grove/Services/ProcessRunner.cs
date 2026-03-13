using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Grove.Models;
using Grove.Services.Abstractions;

namespace Grove.Services;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly Subject<string> _output = new();
    private readonly BehaviorSubject<ProcessStatus> _status = new(ProcessStatus.Idle);
    private readonly ConsoleBuffer _consoleBuffer = new();
    private Process? _process;
    private readonly CompositeDisposable _processSubscriptions = new();

    public string WorktreePath { get; }
    public IObservable<ProcessStatus> Status => _status.AsObservable();
    public ProcessStatus CurrentStatus => _status.Value;
    public DateTimeOffset? StartedAt { get; private set; }
    public ConsoleBuffer ConsoleOutput => _consoleBuffer;

    public ProcessRunner(string worktreePath)
    {
        WorktreePath = worktreePath;
        _consoleBuffer.Attach(_output.AsObservable());
    }

    public void Start(ProcessStartInfo psi)
    {
        if (_process is not null && !_process.HasExited)
            return;

        _status.OnNext(ProcessStatus.Starting);
        StartedAt = DateTimeOffset.Now;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _processSubscriptions.Add(
            Observable.Merge(
                Observable.FromEventPattern<DataReceivedEventHandler, DataReceivedEventArgs>(
                    h => process.OutputDataReceived += h, h => process.OutputDataReceived -= h)
                    .Select(e => e.EventArgs.Data),
                Observable.FromEventPattern<DataReceivedEventHandler, DataReceivedEventArgs>(
                    h => process.ErrorDataReceived += h, h => process.ErrorDataReceived -= h)
                    .Select(e => e.EventArgs.Data))
            .Where(line => line is not null)
            .Subscribe(line => _output.OnNext(line!)));

        _processSubscriptions.Add(
            Observable.FromEventPattern(h => process.Exited += h, h => process.Exited -= h)
                .Take(1)
                .Subscribe(_ =>
                {
                    try
                    {
                        var exitCode = process.ExitCode;
                        _output.OnNext($"\n[grove] Process exited with code {exitCode}");
                        _status.OnNext(exitCode == 0 ? ProcessStatus.Idle : ProcessStatus.Error);
                    }
                    catch { /* process may already be disposed */ }
                    CleanupProcess();
                }));

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;
        _status.OnNext(ProcessStatus.Running);
    }

    public async Task StopAsync()
    {
        await KillAsync();
    }

    public async Task RestartAsync(ProcessStartInfo psi)
    {
        await KillAsync();
        Start(psi);
    }

    private async Task KillAsync()
    {
        var process = _process;
        if (process is null || process.HasExited)
        {
            if (_process is not null) CleanupProcess();
            _status.OnNext(ProcessStatus.Idle);
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // taskkill /T kills the entire process tree (cmd.exe + child)
                using var p = Process.Start(new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                if (p is not null) await p.WaitForExitAsync();
            }
            else
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch { /* best effort — process may have already exited */ }

        // Brief wait for the exit handler to fire
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(cts.Token);
        }
        catch { /* timeout or already disposed — fine */ }

        if (_process is not null)
        {
            _status.OnNext(ProcessStatus.Idle);
            CleanupProcess();
        }
    }

    private void CleanupProcess()
    {
        _processSubscriptions.Clear();
        try { _process?.Dispose(); } catch { /* ignore */ }
        _process = null;
        StartedAt = null;
    }

    public void Dispose()
    {
        try { _process?.Kill(true); } catch { /* best effort */ }
        try { _process?.Dispose(); } catch { /* ignore */ }
        _processSubscriptions.Dispose();
        _consoleBuffer.Dispose();
        _output.Dispose();
        _status.Dispose();
    }
}
