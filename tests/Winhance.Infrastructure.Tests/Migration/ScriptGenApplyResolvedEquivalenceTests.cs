using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 F2a precondition for swapping the autounattend script emitter onto the new catalog model: the
/// NEW RegistryCommandEmitter.ApplyResolvedValuesFromCatalog (reads catalog Setting.Targets / Display.Description) must
/// emit the SAME PowerShell text, byte for byte, as the OLD RegistryCommandEmitter.ApplyResolvedValues (reads
/// SettingDefinition.RegistrySettings / PowerCfgSettings / Description) for every selection the emitter resolves
/// through ComboBox value-mappings. The emitter early-returns on power-plan-selection, so that one is excluded. The
/// SAME resolved values dict - taken straight from ComboBox.Options[idx].ValueMappings (what the old resolver returns
/// and F1 proved the catalog reproduces) - is fed to BOTH methods, for BOTH hive passes, so any divergence is purely
/// in the emitters. Green means flipping AppendSelectionCommandsFiltered onto the new model is provably faithful. Pure
/// - depends only on the catalog, not the machine. Run: dotnet test --filter ScriptGenApplyResolvedEquivalence</summary>
public class ScriptGenApplyResolvedEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenApplyResolvedEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ScriptGenApplyResolvedEquivalence_NewCatalogEmitterMatchesOldByteForByte()
    {
        // One emitter instance, reused; the ILogService mock is never exercised on these paths.
        var emitter = new RegistryCommandEmitter(new Mock<ILogService>().Object);

        // Selections the script emitter resolves via ValueMappings, minus power-plan-selection,
        // on which AppendSelectionCommandsFiltered early-returns - matching the sibling F1 test's filter.
        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection
                     && d.ComboBox?.Options?.Any(o => o.ValueMappings != null) == true
                     && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();

        foreach (var def in selectionDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
            {
                // Unpaired selections-with-ValueMappings are asserted as a regression by
                // ScriptGenSelectionResolveEquivalenceTests; here we only compare the paired ones.
                continue;
            }

            var options = def.ComboBox!.Options;
            for (int idx = 0; idx < options.Count; idx++)
            {
                var mappings = options[idx].ValueMappings;
                if (mappings == null)
                    continue;

                // The SAME dict production feeds ApplyResolvedValues: the option's raw value-mappings,
                // keyed by registry ValueName / "KeyExists" / "PowerCfgValue". Null values are preserved
                // (the null-forgiving operator only retypes object?->object; runtime nulls still flow through,
                // exercising the kvp.Value == null branch in both emitters).
                var dict = mappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

                foreach (var isHkcu in new[] { false, true })
                {
                    var sbOld = new StringBuilder();
                    emitter.ApplyResolvedValues(sbOld, def, dict, isHkcu, "");

                    var sbNew = new StringBuilder();
                    emitter.ApplyResolvedValuesFromCatalog(sbNew, catalogSetting, dict, isHkcu, "");

                    compared.Add($"{def.Id}[{idx}] isHkcu={isHkcu}");

                    var oldText = sbOld.ToString();
                    var newText = sbNew.ToString();
                    if (oldText != newText)
                    {
                        mismatches.Add(
                            $"{def.Id}[{idx}] isHkcu={isHkcu}:\n--- OLD ---\n{oldText}\n--- NEW ---\n{newText}");
                    }
                }
            }
        }

        _output.WriteLine($"{compared.Count} (setting,index,hive) tuples compared, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously.
        Assert.NotEmpty(compared);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} ApplyResolved byte mismatches (new catalog emitter vs old ApplyResolvedValues):\n"
                + string.Join("\n\n", mismatches));
    }

    /// <summary>Defense-in-depth for the F2a flip: the production AppendSelectionCommandsFiltered routes the
    /// CustomStateValues branch through the new emitter too. Unlike a ValueMappings dict (a subset), a CustomStateValues
    /// dict is keyed by the FULL set of a setting's RegistrySetting ValueNames (SystemSettingsDiscoveryService builds
    /// RawValues as `rs.ValueName ?? "KeyExists"`, and AutounattendXmlGeneratorService copies that into CustomStateValues).
    /// The other [Fact] only exercises ValueMappings keys, so cover the superset here: feed a dict carrying every
    /// RegistrySetting ValueName (+ "PowerCfgValue" when powercfg) to BOTH emitters and assert byte-identical. The value
    /// is a fixed non-null constant - equivalence is about old-emitter == new-emitter for the SAME input, not the value
    /// itself. Green confirms every CustomStateValues key matches a catalog Target (the converter's BuildTargets groups
    /// every RegistrySetting by ValueName, enforced structurally by CatalogAuthoringEquivalenceTests).</summary>
    [Fact]
    public void ScriptGenApplyResolvedEquivalence_CustomStateValuesShapedDict_MatchesByteForByte()
    {
        var emitter = new RegistryCommandEmitter(new Mock<ILogService>().Object);

        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection
                     && d.ComboBox?.Options?.Any(o => o.ValueMappings != null) == true
                     && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();

        foreach (var def in selectionDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
                continue;

            // CustomStateValues shape: every RegistrySetting ValueName (or "KeyExists"), + "PowerCfgValue" when powercfg.
            var dict = new Dictionary<string, object>();
            foreach (var rs in def.RegistrySettings)
                dict[rs.ValueName ?? "KeyExists"] = 1;
            if (def.PowerCfgSettings?.Any() == true)
                dict["PowerCfgValue"] = 1;

            if (dict.Count == 0)
                continue;

            foreach (var isHkcu in new[] { false, true })
            {
                var sbOld = new StringBuilder();
                emitter.ApplyResolvedValues(sbOld, def, dict, isHkcu, "");

                var sbNew = new StringBuilder();
                emitter.ApplyResolvedValuesFromCatalog(sbNew, catalogSetting, dict, isHkcu, "");

                compared.Add($"{def.Id} isHkcu={isHkcu}");

                if (sbOld.ToString() != sbNew.ToString())
                    mismatches.Add(
                        $"{def.Id} isHkcu={isHkcu}:\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
            }
        }

        _output.WriteLine($"{compared.Count} CustomStateValues-shaped comparisons, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        Assert.NotEmpty(compared);
        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} CustomStateValues-shape byte mismatches (new catalog emitter vs old):\n"
                + string.Join("\n\n", mismatches));
    }
}
