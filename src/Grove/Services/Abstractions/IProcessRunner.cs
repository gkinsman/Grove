using System.Diagnostics;
using Grove.Models;

namespace Grove.Services.Abstractions;

public interface IProcessRunner : IDisposable
{
    string WorktreePath { get; }
    IObservable<string> Output { get; }
    IObservable<ProcessStatus> Status { get; }
    ProcessStatus CurrentStatus { get; }
    DateTimeOffset? StartedAt { get; }
    ConsoleBuffer ConsoleOutput { get; }

    void Start(ProcessStartInfo psi);
    Task StopAsync(TimeSpan? gracePeriod = null);
    Task RestartAsync(ProcessStartInfo psi, TimeSpan? gracePeriod = null);
}
