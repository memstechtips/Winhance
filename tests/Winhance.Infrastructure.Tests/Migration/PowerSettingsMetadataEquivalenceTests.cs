using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 F4 precondition for the in-place swap in PowerSettingsScriptSection.ExtractPowerSettingsAsync:
/// the metadata it used to read off the OLD SettingDefinition (PowerCfgSettings subgroup/setting GUIDs, RequiresBattery,
/// RequiresBrightnessSupport, Description) must read identically off the NEW catalog Setting (Targets.OfType&lt;PowerCfgTarget&gt;()
/// SubgroupGuid/SettingGuid, Availability.Hardware Battery/BrightnessSupport, Display.Description). The AC/DC values still come
/// from the live power query keyed by the unchanged SettingGuid, so only the metadata reads are compared here. Green means the
/// migrated metadata accessors are a faithful structural equivalence of the old fields. Pure - depends only on the catalog, not
/// the machine. Run: dotnet test --filter PowerSettingsMetadataEquivalence</summary>
public class PowerSettingsMetadataEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerSettingsMetadataEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every old SettingDefinition the app ships, pulled straight from the static feature providers -
    /// the same raw population the sibling Migration equivalence tests use.</summary>
    private static IEnumerable<SettingDefinition> AllDefinitions()
    {
        return new[]
        {
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
        }.SelectMany(group => group);
    }

    [Fact]
    public void PowerSettingsMetadataEquivalence_OldFieldsMatchCatalogReads()
    {
        // Every powercfg-backed def the script section iterates, minus power-plan-selection (skipped first in
        // ExtractPowerSettingsAsync). All powercfg settings ship in the Power feature, but filtering by
        // PowerCfgSettings presence catches any that live elsewhere too.
        var powerDefs = AllDefinitions()
            .Where(d => d.PowerCfgSettings?.Any() == true && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        var unpaired = new List<string>();

        foreach (var def in powerDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
            {
                // Unpaired def: the migration falls back to the old fields, so there is nothing to compare.
                unpaired.Add(def.Id);
                continue;
            }

            compared.Add(def.Id);

            // The (SubgroupGuid, SettingGuid) sequence must be identical and in the same order.
            var oldSeq = def.PowerCfgSettings!
                .Select(p => (p.SubgroupGuid, p.SettingGuid))
                .ToList();
            var newSeq = catalogSetting.Targets.OfType<PowerCfgTarget>()
                .Select(t => (t.SubgroupGuid, t.SettingGuid))
                .ToList();
            if (!oldSeq.SequenceEqual(newSeq))
            {
                mismatches.Add(
                    $"{def.Id} PowerCfg sequence: old=[{string.Join(", ", oldSeq.Select(x => $"{x.SubgroupGuid}/{x.SettingGuid}"))}] "
                        + $"new=[{string.Join(", ", newSeq.Select(x => $"{x.SubgroupGuid}/{x.SettingGuid}"))}]");
            }

            bool newBattery = catalogSetting.Availability.Hardware.Contains(HardwareRequirement.Battery);
            if (def.RequiresBattery != newBattery)
                mismatches.Add($"{def.Id} RequiresBattery: old={def.RequiresBattery} new={newBattery}");

            bool newBrightness = catalogSetting.Availability.Hardware.Contains(HardwareRequirement.BrightnessSupport);
            if (def.RequiresBrightnessSupport != newBrightness)
                mismatches.Add($"{def.Id} RequiresBrightnessSupport: old={def.RequiresBrightnessSupport} new={newBrightness}");

            if (def.Description != catalogSetting.Display.Description)
                mismatches.Add(
                    $"{def.Id} Description: old=\"{def.Description}\" new=\"{catalogSetting.Display.Description}\"");
        }

        _output.WriteLine($"{compared.Count} paired power defs compared, {unpaired.Count} unpaired, {mismatches.Count} mismatches");
        if (unpaired.Count > 0)
            _output.WriteLine("Unpaired (fall back to old fields): " + string.Join(", ", unpaired));
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously.
        Assert.NotEmpty(compared);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} power-settings metadata mismatches (old SettingDefinition vs new catalog Setting):\n"
                + string.Join("\n", mismatches));
    }
}
