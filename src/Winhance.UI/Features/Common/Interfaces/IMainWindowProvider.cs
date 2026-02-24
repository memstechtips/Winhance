using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Provides access to the application's main window without static App.MainWindow coupling.
/// Returns null until the window is created during app startup.
/// </summary>
public interface IMainWindowProvider
{
    Window? MainWindow { get; }
}
