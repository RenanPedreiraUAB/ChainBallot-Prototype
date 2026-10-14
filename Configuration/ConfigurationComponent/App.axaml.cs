using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ConfigurationComponent.Application;
using ConfigurationComponent.ViewModels;
using ConfigurationComponent.Views;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApplication = Avalonia.Application;

namespace ConfigurationComponent;

/// <summary>
/// Represents App.
/// </summary>
public partial class App : AvaloniaApplication
{
    public IServiceProvider? Services { get; private set; }

    /// <summary>
    /// Executes Initialize.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Executes On Framework Initialization Completed.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        Services = new ServiceCollection()
            .AddConfigurationComponent()
            .BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
