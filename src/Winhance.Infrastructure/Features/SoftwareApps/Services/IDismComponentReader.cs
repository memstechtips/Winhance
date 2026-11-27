namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// A null list could not be read; the other may still have been.
internal sealed record InstalledWindowsComponents(HashSet<string>? Capabilities, HashSet<string>? Features);

internal interface IDismComponentReader
{
    // Both lists come from one online session: opening it costs seconds of servicing-stack setup each time. Features are
    // looked up by name, since listing every feature on the image takes seven times as long as asking about the few the catalog has.
    Task<InstalledWindowsComponents> ReadInstalledAsync(bool capabilities, IReadOnlyCollection<string> featureNames, CancellationToken cancellationToken);
}
