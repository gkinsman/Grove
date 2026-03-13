using System.Reactive.Linq;
using Grove.Core.Models;
using Grove.Core.Services.Abstractions;
using ReactiveUI;

namespace Grove.ViewModels;

public class WorktreeViewModel : ViewModelBase
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

    private readonly ObservableAsPropertyHelper<string> _statusColor;
    public string StatusColor => _statusColor.Value;

    private IDisposable? _runnerSubscription;

    public WorktreeViewModel(WorktreeInfo info)
    {
        Info = info;

        _statusColor = this.WhenAnyValue(x => x.Status)
            .Select(s => s switch
            {
                ProcessStatus.Running => "#4EC9B0",
                ProcessStatus.Error => "#F44747",
                ProcessStatus.Starting => "#DCDCAA",
                _ => "#808080"
            })
            .ToProperty(this, x => x.StatusColor);
    }

    public void BindToRunner(IProcessRunner runner)
    {
        // Dispose previous subscription to prevent stacking
        _runnerSubscription?.Dispose();
        _runnerSubscription = runner.Status
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(s => Status = s);
    }

    private static string ShortenPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;
    }
}
