namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetBootstrapper
{
    // Raised after the COM API is verified ready, so subscribers can refresh WinGet-dependent status.
    event EventHandler? WinGetInstalled;

    // False means only the bundled CLI is available.
    bool IsSystemWinGetAvailable { get; }

    Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default);
}
