using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

/// <summary>Phase 6.8 F3 precondition for swapping the autounattend PowerShell-script emitter onto the new catalog
/// model: the NEW FeatureRegistryScriptSection.AppendPowerShellScriptsFromCatalog (reads the catalog Setting's active
/// SettingState ScriptEffects + Display) must emit the SAME BYTES as the OLD AppendPowerShellScripts (reads
/// SettingDefinition.PowerShellScripts EnabledScript/DisabledScript + the old ComboBox options/ScriptVariables) for
/// every script-bearing setting the loop routes through it.
///
/// Scope. Only state-based settings (toggles + selections) are mirrored: the catalog carries scripts on per-state
/// Effects, so the new method keys off the active state. The two script-bearing Action settings
/// (start-menu-clean-10/-11) carry their script as a setting-level Effect with NO states, so the state-based mirror
/// intentionally emits nothing for them while the old loop emits the EnabledScript when IsSelected - exactly the
/// reason the F2b toggle test also excludes the Action settings. Actions are recorded and skipped here; their
/// script emission is a separate Effects-based concern, not this state mirror.
///
/// Faithfulness. The converter has already baked each selection option's preset ScriptVariables into the
/// ScriptEffect.Script and placed Enabled/Disabled/None on the right state (proven by CatalogAuthoringEquivalenceTests
/// for every non-precedence-corrected setting; gaming-touch-keyboard-service's ScriptEffects were verified by hand).
/// So the new method re-applies ONLY the runtime CustomStateValues pass. The SAME ConfigurationItem is fed to BOTH
/// methods, for every option index / selection state and BOTH hive passes, so any divergence is purely in the
/// emitters. Green means flipping the script call site onto the new model is provably faithful. Pure - depends only
/// on the catalog, not the machine. Run: dotnet test --filter ScriptGenPowerShellEquivalence</summary>
public class ScriptGenPowerShellEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenPowerShellEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ScriptGenPowerShellEquivalence_NewCatalogEmitterMatchesOldByteForByte()
    {
        // One section instance, reused; the ILogService mock and RegistryCommandEmitter are not exercised on
        // the AppendPowerShellScripts / AppendPowerShellScriptsFromCatalog paths.
        var logService = new Mock<ILogService>().Object;
        var sut = new FeatureRegistryScriptSection(new RegistryCommandEmitter(logService), logService);

        var scriptDefs = AllDefinitions()
            .Where(d => d.PowerShellScripts != null && d.PowerShellScripts.Count > 0)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        var toggles = new List<string>();
        var selections = new List<string>();
        var actionsSkipped = new List<string>();
        int unpaired = 0;
        int buildGated = 0;

        void Compare(SettingDefinition def, Setting catalogSetting, string label, ConfigurationItem ci, bool isHkcu)
        {
            var sbOld = new StringBuilder();
            sut.AppendPowerShellScripts(sbOld, def, ci, isHkcu, "");

            var sbNew = new StringBuilder();
            sut.AppendPowerShellScriptsFromCatalog(sbNew, catalogSetting, ci, isHkcu, "");

            var tag = $"{def.Id} {label} isHkcu={isHkcu}";
            compared.Add(tag);

            if (sbOld.ToString() != sbNew.ToString())
            {
                mismatches.Add($"{tag}:\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
            }
        }

        foreach (var def in scriptDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
            {
                unpaired++;
                continue;
            }

            // Build-gated (OS-merged) settings carry per-target AppliesTo ranges; none of the script-bearing
            // settings are build-gated today, but keep the guard for parity with the toggle equivalence test.
            if (catalogSetting.Targets.Any(t => t.AppliesTo.Count > 0))
            {
                buildGated++;
                continue;
            }

            // Action scripts live on the catalog's setting-level Effects (no states), so the state-based mirror
            // emits nothing for them while the old loop emits the EnabledScript when selected - out of scope here.
            if (def.InputType == InputType.Action)
            {
                actionsSkipped.Add(def.Id);
                continue;
            }

            if (def.InputType == InputType.Selection)
            {
                selections.Add(def.Id);

                int optionCount = def.ComboBox?.Options.Count ?? catalogSetting.States.Count;
                for (int idx = 0; idx < optionCount; idx++)
                {
                    foreach (var isHkcu in new[] { false, true })
                    {
                        var ci = new ConfigurationItem
                        {
                            Id = def.Id,
                            InputType = def.InputType,
                            SelectedIndex = idx,
                        };
                        Compare(def, catalogSetting, $"option={idx}", ci, isHkcu);
                    }
                }

                // Custom case: locate an option whose catalog ScriptEffect still carries a {{key}} RUNTIME
                // placeholder (the DNS DoH options leave {{dohtemplate}} un-baked because dohtemplate is not in
                // the option's preset ScriptVariables). Feed that key via CustomStateValues at that index and
                // assert both emitters substitute it identically.
                var runtime = FindRuntimePlaceholder(catalogSetting);
                if (runtime is { } rt)
                {
                    foreach (var isHkcu in new[] { false, true })
                    {
                        var ci = new ConfigurationItem
                        {
                            Id = def.Id,
                            InputType = def.InputType,
                            SelectedIndex = rt.StateIndex,
                            CustomStateValues = new Dictionary<string, object>
                            {
                                [rt.Key] = "https://example/dns-query",
                            },
                        };
                        Compare(def, catalogSetting, $"customOption={rt.StateIndex}({rt.Key})", ci, isHkcu);
                    }
                }
            }
            else
            {
                // Toggle / Action-as-toggle handled above; everything left is a toggle.
                toggles.Add(def.Id);

                foreach (var isSelected in new[] { true, false })
                {
                    foreach (var isHkcu in new[] { false, true })
                    {
                        var ci = new ConfigurationItem
                        {
                            Id = def.Id,
                            InputType = def.InputType,
                            IsSelected = isSelected,
                        };
                        Compare(def, catalogSetting, $"isSelected={isSelected}", ci, isHkcu);
                    }
                }
            }
        }

        _output.WriteLine(
            $"{compared.Count} comparisons, {mismatches.Count} mismatches | "
            + $"toggles=[{string.Join(", ", toggles)}] selections=[{string.Join(", ", selections)}] "
            + $"actionsSkipped=[{string.Join(", ", actionsSkipped)}] unpaired={unpaired} buildGated={buildGated}");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously.
        Assert.NotEmpty(compared);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} PowerShell-script byte mismatches (new catalog emitter vs old AppendPowerShellScripts):\n"
                + string.Join("\n\n", mismatches));
    }

    /// <summary>Scans a setting's states for the first ScriptEffect that still carries a <c>{{key}}</c> token - a
    /// runtime-only placeholder the converter did NOT bake (i.e. not in the option's preset ScriptVariables). Returns
    /// the owning state index and the placeholder key, or null if no runtime placeholder survives anywhere.</summary>
    private static (int StateIndex, string Key)? FindRuntimePlaceholder(Setting setting)
    {
        for (int i = 0; i < setting.States.Count; i++)
        {
            foreach (var effect in setting.States[i].Effects.OfType<ScriptEffect>())
            {
                var m = Regex.Match(effect.Script ?? string.Empty, @"\{\{(\w+)\}\}");
                if (m.Success)
                {
                    return (i, m.Groups[1].Value);
                }
            }
        }

        return null;
    }
}
