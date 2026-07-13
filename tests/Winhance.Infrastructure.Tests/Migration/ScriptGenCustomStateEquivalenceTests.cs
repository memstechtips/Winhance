using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
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

/// <summary>Slice 7e-5 precondition for flipping the LAST def-reading script emit onto the catalog: the NEW
/// FeatureRegistryScriptSection.AppendCustomStateScriptsFromCatalog (reads Setting.CustomStateScripts - the
/// un-baked raw EnabledScripts, byte-pinned to def.PowerShellScripts by CustomStateScriptsConformanceTests) must
/// emit the SAME BYTES as the OLD AppendPowerShellScripts (reads the def directly) for the shape the production
/// loop routes to it: a Selection with NO SelectedIndex (a "Custom" value matching no preset option). The SAME
/// ConfigurationItem is fed to BOTH methods, over every shipped Selection def, for every REACHABLE custom shape
/// (no-index + non-empty CustomStateValues, incl. null-valued/non-string/leftover-placeholder entries; no-index
/// + absent AND empty CustomStateValues + IsSelected=true) and BOTH hive passes, so any divergence is purely in
/// the emitters. Green means flipping the Selection-without-index call site onto the new model is provably
/// faithful for every shape that expresses intent.
///
/// The one shape deliberately EXCLUDED from byte-equality is the no-intent shape (no index, no custom values,
/// IsSelected != true): there the old emitter incidentally fell through to the DisabledScript, and the flip
/// deliberately changes that to emitting NOTHING - pinned as a DELTA (not an equivalence) by the
/// BoundedDelta fact below. Pure - depends only on the catalog + old defs, not the machine.
/// Run: dotnet test --filter ScriptGenCustomStateEquivalence</summary>
public class ScriptGenCustomStateEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenCustomStateEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    private static FeatureRegistryScriptSection CreateSut()
    {
        var logService = new Mock<ILogService>().Object;
        return new FeatureRegistryScriptSection(new RegistryCommandEmitter(logService), logService);
    }

    /// <summary>The runtime placeholder keys a def's raw EnabledScripts carry (e.g. primary/secondary/dohtemplate
    /// for gaming-dns-server) - the keys a "Custom" config item substitutes via CustomStateValues.</summary>
    private static IReadOnlyList<string> PlaceholderKeys(SettingDefinition def) =>
        def.PowerShellScripts
            .SelectMany(ps => Regex.Matches(ps.EnabledScript ?? string.Empty, @"\{\{(\w+)\}\}").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

    [Fact]
    public void ScriptGenCustomStateEquivalence_NewCatalogEmitterMatchesOldByteForByte()
    {
        var sut = CreateSut();

        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        var unpaired = new List<string>();
        int nonEmptyOutputs = 0;
        var scriptBearingCompared = new HashSet<string>();

        void Compare(SettingDefinition def, Setting catalogSetting, string label, ConfigurationItem ci, bool isHkcu)
        {
            var sbOld = new StringBuilder();
            sut.AppendPowerShellScripts(sbOld, def, ci, isHkcu, "");

            var sbNew = new StringBuilder();
            sut.AppendCustomStateScriptsFromCatalog(sbNew, catalogSetting, ci, isHkcu, "");

            compared.Add($"{def.Id} {label} isHkcu={isHkcu}");
            if (sbOld.Length > 0)
            {
                nonEmptyOutputs++;
                if (def.PowerShellScripts.Count > 0)
                    scriptBearingCompared.Add(def.Id);
            }

            if (sbOld.ToString() != sbNew.ToString())
                mismatches.Add($"{def.Id} {label} isHkcu={isHkcu}:\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
        }

        foreach (var def in selectionDefs)
        {
            var catalogSetting = SettingCatalog.Find(def.Id);
            if (catalogSetting == null)
            {
                unpaired.Add(def.Id);
                continue;
            }

            // Reachable custom-shape 1: no index + a NON-EMPTY CustomStateValues bag (the user-entered "Custom"
            // export). One value per runtime placeholder key exercises real substitution; the extra entries pin
            // the merge rules both emitters share: an unmatched key (leftover placeholders survive elsewhere), a
            // NULL value (skipped), and a non-string value (ToString()).
            var customValues = new Dictionary<string, object>
            {
                ["unmatchedKey"] = "unused-value",
                ["nullKey"] = null!,
                ["intKey"] = 42,
            };
            foreach (var key in PlaceholderKeys(def))
                customValues[key] = $"custom-{key}";

            // Reachable custom-shape 2: no index + IsSelected=true with ABSENT (null) and EMPTY CustomStateValues
            // (both count as "no custom state" in the old emitter - IsSelected carries the intent).
            var shapes = new (string Label, ConfigurationItem Ci)[]
            {
                ("customValues", new ConfigurationItem { Id = def.Id, InputType = def.InputType, CustomStateValues = customValues }),
                ("isSelected+nullValues", new ConfigurationItem { Id = def.Id, InputType = def.InputType, IsSelected = true }),
                ("isSelected+emptyValues", new ConfigurationItem { Id = def.Id, InputType = def.InputType, IsSelected = true, CustomStateValues = new Dictionary<string, object>() }),
            };

            foreach (var (label, ci) in shapes)
            {
                foreach (var isHkcu in new[] { false, true })
                    Compare(def, catalogSetting, label, ci, isHkcu);
            }
        }

        _output.WriteLine(
            $"{compared.Count} comparisons over {selectionDefs.Count} selections, {mismatches.Count} mismatches, "
            + $"{nonEmptyOutputs} non-empty outputs, script-bearing compared: [{string.Join(", ", scriptBearingCompared.OrderBy(x => x))}], "
            + $"unpaired={unpaired.Count}");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // Vacuity guards: the population must be present, every selection paired, and the 4 script-bearing
        // selections must actually produce output somewhere (an all-empty comparison would prove nothing).
        Assert.NotEmpty(compared);
        Assert.True(unpaired.Count == 0, "selection defs with NO catalog peer: " + string.Join(", ", unpaired));
        Assert.True(nonEmptyOutputs > 0, "every comparison produced empty output - the fact is vacuous");
        Assert.Equal(
            new[] { "explorer-customization-shortcut-arrow", "gaming-dns-server", "gaming-touch-keyboard-service", "taskbar-system-tray-icons-11" },
            scriptBearingCompared.OrderBy(x => x).ToArray());

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} custom-state script byte mismatches (new catalog emitter vs old AppendPowerShellScripts):\n"
                + string.Join("\n\n", mismatches));
    }

    /// <summary>THE PINNED DELTA (deliberate, orchestrator-decided - NOT an equivalence): for the no-intent shape
    /// (Selection, no SelectedIndex, no CustomStateValues, IsSelected != true) the OLD emitter incidentally fell
    /// through to the DisabledScript (useEnabled=false -&gt; script = DisabledScript), so it emitted a Disabled/RESET
    /// block for a config item expressing NO intent - e.g. resetting the user's DNS to automatic. The NEW catalog
    /// path deliberately emits NOTHING for that shape: a no-intent item must not mutate the machine, and the reset
    /// behavior is the riskier of the two. This fact asserts the delta DIRECTLY: new output is empty for every
    /// such shape, old output is the Disabled block exactly when the def carries a non-empty DisabledScript in the
    /// matching hive (arrow's DisabledScript is null, so it has NO delta - both emit nothing).</summary>
    [Fact]
    public void BoundedDelta_NoIntentShape_OldEmitsDisabledScript_NewEmitsNothing()
    {
        var sut = CreateSut();

        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        var violations = new List<string>();
        int realDeltas = 0;

        foreach (var def in selectionDefs)
        {
            var catalogSetting = SettingCatalog.Find(def.Id);
            Assert.NotNull(catalogSetting);

            foreach (var isSelected in new bool?[] { null, false })
            {
                foreach (var isHkcu in new[] { false, true })
                {
                    var ci = new ConfigurationItem { Id = def.Id, InputType = def.InputType, IsSelected = isSelected };

                    var sbOld = new StringBuilder();
                    sut.AppendPowerShellScripts(sbOld, def, ci, isHkcu, "");

                    var sbNew = new StringBuilder();
                    sut.AppendCustomStateScriptsFromCatalog(sbNew, catalogSetting!, ci, isHkcu, "");

                    // NEW side: always nothing for a no-intent shape.
                    if (sbNew.Length != 0)
                        violations.Add($"{def.Id} isSelected={isSelected} isHkcu={isHkcu}: NEW emitted for a no-intent shape:\n{sbNew}");

                    // OLD side: the incidental DisabledScript fall-through - present exactly when the def carries
                    // a non-empty DisabledScript whose RunContext matches this hive pass.
                    bool oldShouldEmit = def.PowerShellScripts.Any(ps =>
                        (ps.RunContext == RunContext.User) == isHkcu && !string.IsNullOrEmpty(ps.DisabledScript));
                    if ((sbOld.Length > 0) != oldShouldEmit)
                        violations.Add($"{def.Id} isSelected={isSelected} isHkcu={isHkcu}: OLD emitted={sbOld.Length > 0} but def DisabledScript presence says {oldShouldEmit}");

                    if (sbOld.Length > 0 && sbNew.Length == 0)
                        realDeltas++;
                }
            }
        }

        _output.WriteLine($"{realDeltas} real (old!=new) no-intent deltas, {violations.Count} violations");

        // Vacuity guard: the delta must actually EXIST - one per (def, shape, hive) comparison where the old
        // side emitted: dns + tray in the user pass, touch-keyboard in the system pass, each twice (isSelected
        // null and false) = 6. (arrow has a null DisabledScript - no delta.) If this vanishes, the old
        // fall-through is gone and this pin should be retired consciously, not silently.
        Assert.Equal(6, realDeltas);
        Assert.True(violations.Count == 0, string.Join("\n\n", violations));
    }
}
