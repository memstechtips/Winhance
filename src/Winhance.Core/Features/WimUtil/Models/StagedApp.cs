namespace Winhance.Core.Features.WimUtil.Models;

// One line of the Generate summary: what the staging step tried to put on the media, and whether it got
// there. An app that did not make it still installs, online, after setup.
public sealed record StagedApp(string Name, bool Staged);
