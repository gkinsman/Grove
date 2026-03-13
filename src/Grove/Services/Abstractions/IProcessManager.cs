using DynamicData;
using Grove.Models;

namespace Grove.Services.Abstractions;

public interface IProcessManager : IDisposable
{
    IObservable<IChangeSet<IProcessRunner, string>> Connect();
    IProcessRunner GetOrCreate(string worktreePath);
    Task StopAllAsync();
    IObservable<ProcessStatus> AggregateStatus { get; }
}
