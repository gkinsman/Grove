using System.Reactive.Linq;
using DynamicData;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;

namespace Grove.Core.Services;

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
                if (!runners.Any()) return ProcessStatus.Idle;
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
        foreach (var runner in _runners.Items)
        {
            try { runner.Dispose(); } catch { /* best effort */ }
        }
        _runners.Dispose();
    }
}
