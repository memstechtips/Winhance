using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 script-gen tail precondition for routing the autounattend Action emission through the new
/// catalog model: the NEW FeatureRegistryScriptSection.AppendActionCommandsFromCatalog (reads the catalog Setting's
/// setting-level Effects - RegistryWriteEffect for the registry pass, ScriptEffect for the script pass) must emit the
/// SAME bytes as the OLD production path for an Action setting, which is AppendToggleCommandsFiltered (registry, run
/// only when IsSelected) followed by AppendPowerShellScripts (scripts), as wired in FeatureRegistryScriptSection's
/// Action branch + the shared script block.
///
/// There are exactly three InputType.Action settings (taskbar-clean, start-menu-clean-10, start-menu-clean-11); the
/// catalog models them as setting-level Effects with no States/Targets. This [Fact] feeds the SAME ConfigurationItem
/// (same IsSelected, NO CustomStateValues) to BOTH paths, for BOTH selection states and BOTH hive passes, so any
/// divergence is purely in the emitters. Comparison is byte-exact: an Action has no mirror RegTargets, so no benign
/// reordering occurs (start-menu-clean-11's two registry writes keep their authored MDM-then-GPO order in both the
/// old RegistrySettings list and the converter-built Effects list). Green means flipping the Action call site onto
/// the new model is provably faithful. Pure - depends only on the catalog, not the machine.
/// Run: dotnet test --filter ScriptGenActionEquivalence</summary>
public class ScriptGenActionEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenActionEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ScriptGenActionEquivalence_NewCatalogEmitterMatchesOldByteForByte()
    {
        var emitter = new RegistryCommandEmitter(new Mock<ILogService>().Object);
        var section = new FeatureRegistryScriptSection(emitter, new Mock<ILogService>().Object);

        var actionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Action)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        int unpaired = 0;

        foreach (var def in actionDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
            {
                unpaired++;
                continue;
            }

            // The lean Action emitter handles only RegistryWriteEffect (registry pass) + ScriptEffect (script pass).
            // If a future Action authors a RegContentEffect / NativePowerEffect, the emitter would silently drop it
            // (and the old path would emit a .reg import) - guard the population so that lands as a RED suite, not a
            // silent autounattend regression.
            Assert.All(
                catalogSetting.Effects,
                e => Assert.True(
                    e is RegistryWriteEffect || e is ScriptEffect,
                    $"Action '{def.Id}' has an Effect of type {e.GetType().Name}; AppendActionCommandsFromCatalog only "
                        + "emits RegistryWriteEffect/ScriptEffect. Extend the emitter (and this test) before shipping."));

            foreach (var isSelected in new[] { true, false })
            {
                foreach (var isHkcu in new[] { false, true })
                {
                    var configItem = new ConfigurationItem
                    {
                        Id = def.Id,
                        IsSelected = isSelected,
                        InputType = def.InputType,
                    };

                    // OLD production-equivalent emission for an Action: the registry pass runs only when the Action is
                    // selected (FeatureRegistryScriptSection's Action branch guards on IsSelected == true), then the
                    // shared script block runs unconditionally (and emits nothing when not enabled).
                    var sbOld = new StringBuilder();
                    if (isSelected == true)
                        emitter.AppendToggleCommandsFiltered(sbOld, def, configItem, isHkcu, "");
                    section.AppendPowerShellScripts(sbOld, def, configItem, isHkcu, "");

                    var sbNew = new StringBuilder();
                    section.AppendActionCommandsFromCatalog(sbNew, catalogSetting, configItem, isHkcu, "");

                    compared.Add($"{def.Id} isSelected={isSelected} isHkcu={isHkcu}");

                    if (sbOld.ToString() != sbNew.ToString())
                    {
                        mismatches.Add(
                            $"{def.Id} isSelected={isSelected} isHkcu={isHkcu}:\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
                    }
                }
            }
        }

        _output.WriteLine($"{compared.Count} (action,isSelected,hive) tuples compared, {mismatches.Count} mismatches, {unpaired} unpaired skipped");

        // 7e-5 review hardening: zero-unpaired pin (see ScriptGenPowerShellEquivalenceTests) - an unpaired
        // Action's registry+script emit would silently drop now that the def fallbacks are gone.
        Assert.True(unpaired == 0, $"{unpaired} Action settings are catalog-unpaired - their emits would silently drop");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously: the three Action settings must be present and paired.
        Assert.NotEmpty(compared);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} Action byte mismatches (new catalog emitter vs old AppendToggleCommandsFiltered + AppendPowerShellScripts):\n"
                + string.Join("\n\n", mismatches));
    }
}
