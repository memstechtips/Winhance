using Microsoft.UI.Xaml;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class MainWindowProvider : IMainWindowProvider
{
    public Window? MainWindow => App.MainWindow;
}
