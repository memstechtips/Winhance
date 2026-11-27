using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ICoverageSummaryService
{
    // Closes every import. For the user's own file it also lists what the file lacks against this version of
    // Winhance and offers an updated copy; a preset or a backup gets only the applied, set-aside and not-applied report.
    // Every import names the applied settings that take effect only after a restart.
    Task ShowImportSummaryAsync(WinhanceConfigFile applied, string? sourcePath, IReadOnlyList<string> setAside, IReadOnlyList<string> notApplied);

    // The settings this version has that a user's profile leaves out, once per version until the user turns it off.
    // True when the user chose to edit the profile in Builder.
    Task<bool> ShowProfileReminderAsync(SettingsProfile profile, WinhanceConfigFile config);

    // The tray's version: a count for a notification, and a notification has no checkbox, so it counts as shown.
    Task<int> TakeProfileReminderAsync(SettingsProfile profile, WinhanceConfigFile config);
}
