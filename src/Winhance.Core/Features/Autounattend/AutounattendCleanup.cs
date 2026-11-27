using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.Autounattend;

// The cleanup the file runs after setup frees the same things the image cleanup frees offline, so the cards
// are that option set.
public static class AutounattendCleanup
{
    public const string SettingId = "autounattend-cleanup";

    public static bool Wanted(SelectionSet set) => set.StateOf(SettingId)?.Label == LocKey.Common.Enabled;

    public static ImageCleanupOptions Options(SelectionSet set) => new(
        ResetBase: Ticked(set, "autounattend-cleanup-old-components"),
        DownloadedUpdates: Ticked(set, "autounattend-cleanup-update-downloads"),
        ServicingLogs: Ticked(set, "autounattend-cleanup-servicing-logs"),
        TempFiles: Ticked(set, "autounattend-cleanup-temp-files"),
        CrashReports: Ticked(set, "autounattend-cleanup-crash-reports"));

    private static bool Ticked(SelectionSet set, string settingId) =>
        set.StateOf(settingId)?.Label == LocKey.Common.Checked;
}
