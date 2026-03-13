using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;

namespace Grove.Core.Services;

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
        if (_process is not null && !_process.HasExited)
            return; // prevent double-start

        _status.OnNext(ProcessStatus.Starting);
        StartedAt = DateTimeOffset.Now;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Bridge stdout to Rx
        var stdout = Observable.FromEventPattern<DataReceivedEventHandler, DataReceivedEventArgs>(
            h => process.OutputDataReceived += h,
            h => process.OutputDataReceived -= h)
            .Select(e => e.EventArgs.Data);

        // Bridge stderr to Rx
        var stderr = Observable.FromEventPattern<DataReceivedEventHandler, DataReceivedEventArgs>(
            h => process.ErrorDataReceived += h,
            h => process.ErrorDataReceived -= h)
            .Select(e => e.EventArgs.Data);

        // Bridge exit event to Rx
        var exited = Observable.FromEventPattern(
            h => process.Exited += h,
            h => process.Exited -= h);

        _processSubscriptions.Add(
            Observable.Merge(stdout, stderr)
                .Where(line => line is not null)
                .Subscribe(line => _output.OnNext(line!)));

        _processSubscriptions.Add(
            exited.Take(1).Subscribe(_ =>
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

    public async Task StopAsync(TimeSpan? gracePeriod = null)
    {
        if (_process is null || _process.HasExited)
        {
            _status.OnNext(ProcessStatus.Idle);
            return;
        }

        var grace = gracePeriod ?? TimeSpan.FromSeconds(5);
        _status.OnNext(ProcessStatus.Stopped);

        if (OperatingSystem.IsWindows())
        {
            await RunTaskkillAsync(_process.Id, force: false);
            var exited = await WaitForExitAsync(_process, grace);
            if (!exited)
                await RunTaskkillAsync(_process.Id, force: true);
        }
        else
        {
            try { _process.Kill(false); } catch { /* best effort SIGTERM */ }
            var exited = await WaitForExitAsync(_process, grace);
            if (!exited)
            {
                try { _process.Kill(true); } catch { /* best effort SIGKILL */ }
            }
        }

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
        try { _process?.Dispose(); } catch { /* ignore */ }
        _process = null;
        StartedAt = null;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task RunTaskkillAsync(int pid, bool force)
    {
        var args = force ? $"/PID {pid} /T /F" : $"/PID {pid} /T";
        try
        {
            using var p = Process.Start(new ProcessStartInfo("taskkill", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is not null) await p.WaitForExitAsync();
        }
        catch { /* best effort */ }
    }

    public void Dispose()
    {
        try { _process?.Kill(true); } catch { /* best effort */ }
        try { _process?.Dispose(); } catch { /* ignore */ }
        _processSubscriptions.Dispose();
        _output.Dispose();
        _status.Dispose();
    }
}
