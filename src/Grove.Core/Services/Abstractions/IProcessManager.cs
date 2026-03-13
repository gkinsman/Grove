using DynamicData;
using Grove.Core.Models;

namespace Grove.Core.Services.Abstractions;

public interface IProcessManager : IDisposable
{
    IObservable<IChangeSet<IProcessRunner, string>> Connect();
    IProcessRunner GetOrCreate(string worktreePath);
    Task StopAllAsync();
    IObservable<ProcessStatus> AggregateStatus { get; }
}
