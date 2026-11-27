namespace Winhance.Core.Features.WimUtil.Models;

// Failed names the apps or settings that did not apply, so the answer file can still handle them during setup.
// Applied counts the writes that did land, so a partly failed step still has its work committed.
public sealed record OfflineStepOutcome(bool Succeeded, IReadOnlyList<string> Failed, int Applied);
