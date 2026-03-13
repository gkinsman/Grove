using Grove.Core.Services;
using Grove.Core.Services.Abstractions;
using Grove.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Grove;

public static class Bootstrapper
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IProcessManager, ProcessManager>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<TrayViewModel>();

        return services.BuildServiceProvider();
    }
}
