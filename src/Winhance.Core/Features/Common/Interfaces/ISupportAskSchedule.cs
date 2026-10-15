namespace Winhance.Core.Features.Common.Interfaces;

// When the exit-time support ask may show: at most once a day. A Yes only opens the store, so it earns no quiet time.
public interface ISupportAskSchedule
{
    Task<bool> ShouldAskOnExitAsync();

    Task MarkAskedAsync();
}
