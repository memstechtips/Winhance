namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetBootstrapper
{
    // False means only the bundled CLI is available.
    bool IsSystemWinGetAvailable { get; }

    Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default);
}
