using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Interfaces;

// Null until the window is created during startup.
public interface IMainWindowProvider
{
    Window? MainWindow { get; }
}
