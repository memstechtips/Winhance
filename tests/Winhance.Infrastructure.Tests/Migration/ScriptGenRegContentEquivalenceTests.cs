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

/// <summary>Phase 6.8 F2c precondition for swapping the autounattend RegContents emitter onto the new catalog model:
/// the NEW RegistryCommandEmitter.AppendRegContentCommandsFromCatalog (reads the catalog Setting's active SettingState
/// RegContentEffects / Display.Description) must emit the SAME PowerShell .reg-import block, BYTE-FOR-BYTE, as the OLD
/// RegistryCommandEmitter.AppendRegContentCommands (reads SettingDefinition.RegContents' EnabledContent/DisabledContent
/// / Description) for every NON-build-gated RegContent-bearing toggle.
///
/// Unlike the toggle registry mirror, RegContents is a single ORDERED list with no mirror-grouping reorder: the
/// converter's BuildToggleEffects appends one RegContentEffect per non-empty RegContents[i].EnabledContent (in order)
/// to the Enabled state, and per non-empty DisabledContent to the Disabled state, so the active state's
/// RegContentEffects are exactly the old method's selected non-empty contents in the same order. The per-content
/// emission (mixed-hive rejection, hive routing, the try/heredoc/reg-import/Write-Log block) is identical, so a
/// straight byte comparison is meaningful and any reorder/extra/missing/changed line fails. Both methods are fed the
/// SAME isEnabled and hive pass, so any divergence is purely in the emitters. Green means flipping the RegContents call
/// site onto the new model is provably faithful. Pure - depends only on the catalog, not the machine.
/// Run: dotnet test --filter ScriptGenRegContentEquivalence</summary>
public class ScriptGenRegContentEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenRegContentEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ScriptGenRegContentEquivalence_NewCatalogEmitterMatchesOldByteForByte()
    {
        // One emitter instance, reused; the ILogService mock is never exercised on these paths.
        var emitter = new RegistryCommandEmitter(new Mock<ILogService>().Object);

        // RegContent-bearing toggles only: the OLD AppendRegContentCommands and the NEW
        // AppendRegContentCommandsFromCatalog both emit ONLY the .reg-import blocks, so byte equality is meaningful.
        var regContentToggleDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Toggle
                     && d.RegContents != null
                     && d.RegContents.Count > 0)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        int unpaired = 0;
        int buildGated = 0;

        foreach (var def in regContentToggleDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
            {
                unpaired++;
                continue;
            }

            // Build-gated settings (per-target AppliesTo Win10/Win11 ranges) stay on the OLD emitter in the flip:
            // AppendRegContentCommandsFromCatalog has no build context. Skip them here, matching the toggle test.
            if (catalogSetting.Targets.Any(t => t.AppliesTo.Count > 0))
            {
                buildGated++;
                continue;
            }

            foreach (var isEnabled in new[] { true, false })
            {
                foreach (var isHkcu in new[] { false, true })
                {
                    var sbOld = new StringBuilder();
                    emitter.AppendRegContentCommands(sbOld, def, isEnabled, isHkcu, "");

                    var sbNew = new StringBuilder();
                    emitter.AppendRegContentCommandsFromCatalog(sbNew, catalogSetting, isEnabled, isHkcu, "");

                    compared.Add($"{def.Id} isEnabled={isEnabled} isHkcu={isHkcu}");

                    // RegContents is a single ordered list - no mirror-reorder - so compare BYTE-EXACT.
                    if (sbOld.ToString() != sbNew.ToString())
                    {
                        mismatches.Add(
                            $"{def.Id} isEnabled={isEnabled} isHkcu={isHkcu}:\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
                    }
                }
            }
        }

        _output.WriteLine($"{compared.Count} (setting,isEnabled,hive) tuples compared, {mismatches.Count} mismatches, {unpaired} unpaired skipped, {buildGated} build-gated skipped");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously.
        Assert.NotEmpty(compared);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} RegContent byte mismatches (new catalog emitter vs old AppendRegContentCommands):\n"
                + string.Join("\n\n", mismatches));
    }
}
