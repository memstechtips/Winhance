namespace Winhance.Core.Features.WimUtil.Models;

// One line of the Generate summary: what the staging step tried to put on the media, and what became of it.
// An app from the catalog that did not make it still installs, online, after setup; an own installer does not.
public sealed record StagedApp(string Name, StagingOutcome Outcome)
{
    public bool Staged => Outcome == StagingOutcome.OnMedia;
}

// Ordered best first: an app tried several ways reports the best outcome any attempt reached.
public enum StagingOutcome
{
    OnMedia,
    NeedsWinGet,
    DownloadFailed,
    NoInstallerForArchitecture,
    NotFound,
    CopyFailed,
    Skipped,
}
