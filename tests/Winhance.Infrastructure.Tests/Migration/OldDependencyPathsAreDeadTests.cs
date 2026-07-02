using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Locks the precondition for retiring the old dependency resolver: SettingApplicationService runs the
/// OLD dependency / value-prerequisite / inline-preset paths ONLY for settings its EXACT-match `paired` check
/// treats as unpaired (SettingCatalog.All.Any(s => s.Id == settingId)). The only such settings are the 6 -win10
/// ThisPC aliases (every other setting has its exact id in the catalog). This proves those aliases carry NO
/// Dependencies / AutoEnableSettingIds / SettingPresets - so the old paths are no-ops and SettingDependencyResolver
/// + DependencyManager are dead-in-effect and safe to delete. If a future unpaired setting gains a dependency, this
/// fails - a signal to normalize the paired check (route it through the new RelationshipResolver) before deleting.</summary>
public class OldDependencyPathsAreDeadTests
{
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
    public void Exact_match_unpaired_settings_carry_no_dependencies_or_presets()
    {
        var catalogIds = SettingCatalog.All.Select(s => s.Id).ToHashSet();
        var unpaired = AllOldDefs().Where(d => !catalogIds.Contains(d.Id)).ToList();

        // The exact-match unpaired set is exactly the 6 -win10 ThisPC aliases.
        Assert.NotEmpty(unpaired);
        Assert.All(unpaired, d => Assert.EndsWith("-win10", d.Id));

        var offenders = unpaired.Where(d =>
            d.Dependencies.Count > 0
            || (d.AutoEnableSettingIds is { Count: > 0 })
            || (d.SettingPresets is { Count: > 0 }))
            .Select(d => d.Id).ToList();
        Assert.True(offenders.Count == 0,
            "Unpaired settings with dependencies/presets (old resolver NOT dead - normalize `paired` first): " + string.Join(", ", offenders));
    }

    [Fact]
    public void Special_handler_settings_have_no_dependencies_so_the_parent_sync_is_dead()
    {
        // The one dependency-resolver call NOT gated by !paired is SyncParentToMatchingPresetAsync in SAS's
        // special-handler success path (line ~203). It early-returns unless the setting has a
        // RequiresValueBeforeAnyChange dependency. The special-handler settings carry no dependencies, so that
        // call is a no-op too - completing the proof that the old resolver is dead at EVERY SAS call site.
        foreach (var id in new[] { SettingIds.UpdatesPolicyMode, SettingIds.ThemeModeWindows })
        {
            var def = AllOldDefs().FirstOrDefault(d => d.Id == id);
            Assert.NotNull(def);
            Assert.Empty(def!.Dependencies);
        }
    }
}
