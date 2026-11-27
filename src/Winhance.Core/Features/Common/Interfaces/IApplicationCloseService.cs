using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IApplicationCloseService
{
    Func<Task>? BeforeShutdown { get; set; }
    Task<OperationResult> CheckOperationsAndCloseAsync();

    // Saves and leaves without the running-task or support dialogs.
    Task ExitAsync();

    // Exit from the tray with no window on screen: the daily support ask shows in its own small window.
    Task ExitWithoutWindowAsync();
}
