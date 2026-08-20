using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// The APPLY direction: pressing "Apply Windows Defaults" on a clean machine must leave it clean.
// Of(x).OrAbsent() relaxes reading only, so a state can detect green and still stamp a value onto a target
// Windows ships clean; SettingState.ResetSet is the cure and this keeps it applied. Each fixture pins its
// expected writers exactly. Run: winhance-harness CatalogDefaultApplyConformanceTests
public class CatalogDefaultApplyConformanceTests
{
    private const string Held =
        "held: this target's detection is pinned divergent too - resolve both together (CatalogCleanInstallConformanceTests)";
    private const string Unswept =
        "unswept: outside the five-capture intersection the ResetSet sweep covered; needs its own evidence pass";

    private static readonly IReadOnlyDictionary<string, string> Win10VmExpected = new Dictionary<string, string>
    {
        ["explorer-customization-compressed-color/ShowEncryptCompressedColor"] = Unswept,
        ["explorer-customization-desktop-icon-recycle-bin/{645FF040-5081-101B-9F08-00AA002F954E}"] = Unswept,
        ["explorer-customization-nav-expand-current/NavPaneExpandToCurrentFolder"] = Unswept,
        ["explorer-customization-show-menus/AlwaysShowMenus"] = Unswept,
        ["gaming-explorer-alt-tab-filter/MultiTaskingAltTabFilter"] = Unswept,
        ["gaming-memory-integrity/Enabled"] = Held,
        ["gaming-memory-integrity/WasEnabledBy"] = Held,
        ["gaming-virtualization-based-security/EnableVirtualizationBasedSecurity"] = Held,
        ["gaming-virtualization-based-security/RequirePlatformSecurityFeatures"] = Held,
        ["gaming-xbox-game-dvr/AllowGameDVR"] = Unswept,
        ["gaming-xbox-game-dvr/AppCaptureEnabled"] = Unswept,
        ["notifications-app-location-request/ShowGlobalPrompts"] = Unswept,
        ["notifications-show-bell-icon/ShowNotificationIcon"] = Unswept,
        ["power-hibernation-enable/HibernateEnabled"] = Unswept,
        ["power-throttling/PowerThrottlingOff"] = Unswept,
        ["privacy-improve-inking-typing/Value"] = Unswept,
        ["privacy-inking-typing-dictionary/Value"] = Held,
        ["privacy-location-services/DisableLocation"] = Unswept,
        ["security-automatic-maintenance/MaintenanceDisabled"] = Unswept,
        ["security-error-reporting/Disabled"] = Unswept,
        ["start-power-hibernate-option/ShowHibernateOption"] = Unswept,
        ["taskbar-small/TaskbarSmallIcons"] = Unswept,
        ["taskbar-system-tray-icons/EnableAutoTray"] = Unswept,
    };

    private static readonly IReadOnlyDictionary<string, string> GoldLaptopExpected = new Dictionary<string, string>
    {
        ["explorer-customization-item-space/UseCompactMode"] = Unswept,
        ["gaming-directx-auto-hdr/DirectXUserGlobalSettings"] = Unswept,
        ["gaming-virtualization-based-security/RequirePlatformSecurityFeatures"] = Held,
        ["privacy-advertising-id/DisabledByGroupPolicy"] = Unswept,
        ["privacy-advertising-id/Enabled"] = Unswept,
        ["privacy-inking-typing-dictionary/Value"] = Held,
        ["privacy-turn-off-copilot/TurnOffWindowsCopilot"] = Unswept,
        ["taskbar-end-task/TaskbarEndTask"] = Unswept,
    };

    private static readonly IReadOnlyDictionary<string, string> PostUpdateVmExpected = new Dictionary<string, string>
    {
        ["explorer-customization-compressed-color/ShowEncryptCompressedColor"] = Unswept,
        ["explorer-customization-desktop-icon-recycle-bin/{645FF040-5081-101B-9F08-00AA002F954E}"] = Unswept,
        ["explorer-customization-item-space/UseCompactMode"] = Unswept,
        ["explorer-customization-nav-expand-current/NavPaneExpandToCurrentFolder"] = Unswept,
        ["gaming-directx-auto-hdr/DirectXUserGlobalSettings"] = Unswept,
        ["gaming-explorer-alt-tab-filter/MultiTaskingAltTabFilter"] = Unswept,
        ["gaming-memory-integrity/Enabled"] = Held,
        ["gaming-memory-integrity/WasEnabledBy"] = Held,
        ["gaming-virtualization-based-security/EnableVirtualizationBasedSecurity"] = Held,
        ["gaming-virtualization-based-security/RequirePlatformSecurityFeatures"] = Held,
        ["gaming-xbox-game-dvr/AllowGameDVR"] = Unswept,
        ["gaming-xbox-game-dvr/AppCaptureEnabled"] = Unswept,
        ["notifications-app-location-request/ShowGlobalPrompts"] = Unswept,
        ["notifications-show-bell-icon/ShowNotificationIcon"] = Unswept,
        ["power-hibernation-enable/HibernateEnabled"] = Unswept,
        ["power-throttling/PowerThrottlingOff"] = Unswept,
        ["privacy-disable-input-insights/InsightsEnabled"] = Unswept,
        ["privacy-improve-inking-typing/Enabled"] = Unswept,
        ["privacy-improve-inking-typing/Value"] = Unswept,
        ["privacy-inking-typing-dictionary/Value"] = Held,
        ["security-automatic-maintenance/MaintenanceDisabled"] = Unswept,
        ["security-error-reporting/Disabled"] = Unswept,
        ["start-power-hibernate-option/ShowHibernateOption"] = Unswept,
        ["taskbar-end-task/TaskbarEndTask"] = Unswept,
        ["updates-metered-connection/AllowAutoWindowsUpdateDownloadOverMeteredNetwork"] = Unswept,
        ["updates-restart-asap/IsExpedited"] = Unswept,
    };

    [Fact]
    public void Win10_22H2_vm_windows_defaults_write_nothing_it_shipped_without()
        => RunFixture("cleaninstall-win10-22h2-pro-vm.json", Win10VmExpected);

    [Fact]
    public void Win11_25H2_gold_laptop_windows_defaults_write_nothing_it_shipped_without()
        => RunFixture("cleaninstall-win11-25h2-gold-laptop.json", GoldLaptopExpected);

    [Fact]
    public void Win11_25H2_post_update_vm_windows_defaults_write_nothing_it_shipped_without()
        => RunFixture("cleaninstall-win11-25h2-post-update-vm.json", PostUpdateVmExpected);

    private static void RunFixture(string fixtureName, IReadOnlyDictionary<string, string> expectedWriters)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath(fixtureName)));
        var machine = doc.RootElement.GetProperty("machine");
        var build = new WinBuild(machine.GetProperty("buildNumber").GetInt32(), machine.GetProperty("ubr").GetInt32());

        var writers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var suspect in doc.RootElement.GetProperty("absentWindowsDefaultSuspects").EnumerateArray())
        {
            if (!suspect.GetProperty("countsTowardFinding").GetBoolean()) continue;

            var settingId = suspect.GetProperty("id").GetString()!;
            var targetKey = suspect.GetProperty("targetKey").GetString()!;

            var setting = SettingCatalog.All.FirstOrDefault(s => s.Id == settingId);
            if (setting is null) continue;                            // catalog drifted past the capture
            if (!setting.Availability.Allows(build)) continue;
            if (!setting.Targets.OfType<RegTarget>().Any(t => t.Key == targetKey)) continue;

            var defaultLabel = setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault, build))?.Label;
            if (defaultLabel is null) continue;

            if (ApplyPlanBuilder.Build(setting, defaultLabel, build, reset: true).Any(op => IsWriteTo(op, targetKey)))
                writers.Add($"{settingId}/{targetKey}");
        }

        var unexpected = writers.Where(w => !expectedWriters.ContainsKey(w)).ToList();
        var swept = expectedWriters.Keys.Where(k => !writers.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"Applying Windows defaults on {fixtureName} WRITES where that machine shipped clean:\n  "
            + string.Join("\n  ", unexpected)
            + "\nGive each one a ResetSet on its WindowsDefault state, or pin it with a reason.");
        Assert.True(swept.Count == 0,
            $"PINNED writers no longer write on {fixtureName} (remove them from the expected set):\n  "
            + string.Join("\n  ", swept));
    }

    // Lock/unlock ops manage key permissions around a write rather than producing a value, so they do not
    // dirty a clean target on their own; the deletes are the whole point of a ResetSet.
    private static bool IsWriteTo(ApplyOp op, string targetKey) => op switch
    {
        RegistryDeleteOp or RegistryPerSubkeyDeleteOp or RegistryUnlockKeyOp or RegistryLockKeyOp => false,
        RegistryWriteOp w => w.Target.Key == targetKey,
        RegistryEnsureKeyOp e => e.Target.Key == targetKey,
        RegistryPerSubkeyWriteOp p => p.Target.Key == targetKey,
        RegistryBitSetOp b => b.Target.Key == targetKey,
        RegistryByteSetOp y => y.Target.Key == targetKey,
        RegistryCompositeSetOp c => c.Target.Key == targetKey,
        RegistryStringFlagSetOp f => f.Target.Key == targetKey,
        _ => false,
    };

    private static string FixturePath(string name)
        => Path.Combine(RepoPaths.SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Catalog", "Fixtures", name);
}
