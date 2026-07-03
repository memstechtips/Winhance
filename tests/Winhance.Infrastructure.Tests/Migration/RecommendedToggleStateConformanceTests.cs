using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>
/// Proves the catalog-derived <see cref="CatalogToggleState"/> recommended/default toggle state equals the old
/// <see cref="SettingDefinitionToggleState"/> result for EVERY catalog-paired setting, resolved at the build the
/// old def is the authority for - the machine-independent precondition for repointing RecommendedSettingsResolver /
/// RecommendedSettingsApplier / BulkSettingsActionService off the old helper onto the catalog roles (Slice C).
/// The 6 merged "This PC folder" settings carry an OS-divergent Windows default (shown on Win10, hidden on Win11)
/// as build-scoped WindowsDefault roles, so this asserts each old per-OS def against the catalog default resolved
/// for THAT OS. Reads the old <c>.Settings</c> + <c>SettingCatalog.All</c>, not the converter, so it survives the
/// converter teardown as a permanent conformance guard.
/// </summary>
public class RecommendedToggleStateConformanceTests
{
    private static readonly WinBuild Win10Build = new(19045);
    private static readonly WinBuild Win11Build = new(22631);

    private static IEnumerable<SettingDefinition> AllOldDefs() =>
        ExplorerCustomizations.GetExplorerCustomizations().Settings
        .Concat(StartMenuCustomizations.GetStartMenuCustomizations().Settings)
        .Concat(TaskbarCustomizations.GetTaskbarCustomizations().Settings)
        .Concat(WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings)
        .Concat(PowerOptimizations.GetPowerOptimizations().Settings)
        .Concat(GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings)
        .Concat(NotificationOptimizations.GetNotificationOptimizations().Settings)
        .Concat(PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings)
        .Concat(SoundOptimizations.GetSoundOptimizations().Settings)
        .Concat(UpdateOptimizations.GetUpdateOptimizations().Settings);

    [Fact]
    public void Catalog_toggle_state_matches_old_SettingDefinitionToggleState_for_every_paired_setting()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        foreach (var def in AllOldDefs())
        {
            // Actions are modeled effect-only (no Enabled/Disabled states) and are excluded from bulk apply/reset
            // (commit 7000de12), so their old primary-registry toggle-state is dead - out of scope for this gate.
            if (def.InputType == InputType.Action)
                continue;
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue; // unpaired def (no catalog peer) - out of scope

            compared++;

            // The build at which THIS old def is the authority for its default: its OS-only flag (a merged
            // setting's canonical def is Win11-only, its -win10 def Win10-only). For every unconditional-role
            // setting the build is irrelevant (an empty-AppliesTo role matches any build), so any value is fine.
            var build = def.IsWindows10Only ? Win10Build
                : def.IsWindows11Only ? Win11Build
                : Win11Build;

            var oldRec = SettingDefinitionToggleState.GetRecommendedToggleState(def);
            var newRec = CatalogToggleState.GetRecommended(s, build);
            if (newRec != oldRec)
                mismatches.Add($"{def.Id} @build{build.Build}: recommended new {Fmt(newRec)} != old {Fmt(oldRec)}");

            var oldDef = SettingDefinitionToggleState.GetDefaultToggleState(def);
            var newDef = CatalogToggleState.GetDefault(s, build);
            if (newDef != oldDef)
                mismatches.Add($"{def.Id} @build{build.Build}: default new {Fmt(newDef)} != old {Fmt(oldDef)}");
        }

        Assert.True(compared > 300, $"only compared {compared} settings - population scoping bug");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} toggle-state mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(bool? b) => b?.ToString() ?? "null";
}
