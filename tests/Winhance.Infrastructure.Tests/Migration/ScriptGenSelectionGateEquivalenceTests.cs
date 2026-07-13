using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice E1c precondition: the script-gen Selection READING gates are moving off the old
/// SettingDefinition (InputType / ComboBox.Options.ValueMappings) onto the catalog model
/// (Setting.Control / SettingState.Set). This proves the three swapped decisions are old-vs-new IDENTICAL
/// over the whole shipped selection population, machine-independently (catalog + old defs only, no I/O):
///
///   1. DISPATCH (FeatureRegistryScriptSection: the Selection call-site + selectionWithoutIndex, which since
///      Slice 7e-5 routes between AppendPowerShellScriptsFromCatalog and AppendCustomStateScriptsFromCatalog):
///      the routing "is this a Selection?" moves from
///      configItem/settingDef.InputType == InputType.Selection to catalog Control == ControlKind.Selection.
///      Faithful iff, over the reachable population (power-plan-selection is skipped upstream, being the only
///      OptionSource setting), InputType.Selection &lt;=&gt; Control == Selection. (ControlDerivationConformanceTests
///      proves Control tracks InputType; this re-proves the Selection slice of it directly and bidirectionally.)
///
///   2. GATE A (RegistryCommandEmitter.AppendSelectionCommandsFiltered): "does this selection resolve via
///      value-mappings?" moves from def.ComboBox.Options.Any(o =&gt; o.ValueMappings != null) to
///      catalogSetting.States.Any(st =&gt; st.Set.Count &gt; 0). Faithful iff the two are equal per setting - i.e.
///      an option's ValueMappings become exactly a non-empty state Set (the converter builds Set from
///      ValueMappings; a script-only selection - system-tray / dns - has empty Sets, matching no ValueMappings).
///
///   3. GATE B (RegistryCommandEmitter.ResolveSelectionValuesFromCatalog): the per-index "the SELECTED option
///      carries value-mappings" moves from options[i].ValueMappings == null to States[i].Set.Count == 0.
///      Faithful iff, per option index, options[i].ValueMappings == null &lt;=&gt; States[i].Set.Count == 0. This
///      test checks States.Count == Options.Count + per-index null-alignment; the per-index VALUE equivalence is
///      proven by the sibling ScriptGenSelectionResolveEquivalenceTests.
///
/// Green means the E1c reader swaps are provably behaviour-preserving. Pure - depends only on the catalog.
/// Run: dotnet test --filter ScriptGenSelectionGateEquivalence</summary>
public class ScriptGenSelectionGateEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenSelectionGateEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every old SettingDefinition the app ships, the same raw population the sibling Migration
    /// equivalence tests use.</summary>
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

    /// <summary>DISPATCH swap: over every shipped def (excluding power-plan-selection, which the emitter skips
    /// upstream - it is the only OptionSource setting, so it derives Control == PowerPlan not Selection),
    /// catalog Control == ControlKind.Selection is IDENTICAL to old InputType == InputType.Selection. Both
    /// directions: every Selection def pairs and derives Control.Selection, and no non-Selection def does. This is
    /// what makes routing the Selection call-site / selectionWithoutIndex (since 7e-5: the custom-state emitter's
    /// dispatch) off InputType onto Control faithful.</summary>
    [Fact]
    public void DispatchSwap_CatalogControlSelection_MatchesOldInputTypeSelection()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var comparedIds = new List<string>();
        var unpairedSelections = new List<string>();

        foreach (var def in AllDefinitions())
        {
            if (def.Id == SettingIds.PowerPlanSelection)
                continue; // skipped by the emitter (FeatureRegistryScriptSection); the only OptionSource -> PowerPlan

            var normalizedId = SettingIdAliases.Normalize(def.Id);
            if (!catalogById.TryGetValue(normalizedId, out var s))
            {
                // Since 7e-5 the script pass is catalog-ALWAYS: an unpaired id emits nothing (the def fallback is
                // gone, matching the 7e-4b/4c unpaired->skip semantics). An unpaired SELECTION would therefore
                // silently drop its scripts - surface it (the catalog is expected complete for the shipped
                // population; zero unpaired today).
                if (def.InputType == InputType.Selection)
                    unpairedSelections.Add(def.Id);
                continue;
            }

            comparedIds.Add(def.Id);
            bool oldIsSelection = def.InputType == InputType.Selection;
            bool newIsSelection = s.Control == ControlKind.Selection;
            if (oldIsSelection != newIsSelection)
                mismatches.Add($"{def.Id}: old InputType.Selection={oldIsSelection} != new Control.Selection={newIsSelection} (InputType={def.InputType}, Control={s.Control})");
        }

        foreach (var id in unpairedSelections)
            _output.WriteLine($"[unpaired-selection] {id}");
        _output.WriteLine($"{comparedIds.Count} settings compared, {unpairedSelections.Count} unpaired selections, {mismatches.Count} dispatch mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(comparedIds.Count > 300, $"only compared {comparedIds.Count} settings - population scoping bug");
        Assert.True(unpairedSelections.Count == 0, "selection defs with NO catalog peer (the catalog-always script pass would emit NOTHING for them): " + string.Join(", ", unpairedSelections));
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} dispatch mismatches (catalog Control.Selection vs old InputType.Selection):\n" + string.Join("\n", mismatches));
    }

    /// <summary>GATE A + GATE B swap: for every Selection def (minus power-plan-selection, on which
    /// AppendSelectionCommandsFiltered early-returns), the new catalog shape checks reproduce the old ComboBox
    /// value-mapping gates EXACTLY. Gate A: Any option ValueMappings != null &lt;=&gt; Any state Set non-empty.
    /// Gate B (per option index): options[i].ValueMappings == null &lt;=&gt; States[i].Set.Count == 0, with
    /// States.Count == Options.Count + per-index null-alignment (per-index VALUE equivalence is the sibling
    /// ScriptGenSelectionResolveEquivalenceTests' job).</summary>
    [Fact]
    public void SelectionGateSwap_CatalogStateSet_MatchesOldComboBoxValueMappings()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var comparedIds = new List<string>();
        var unpaired = new List<string>();
        int gateBIndicesCompared = 0;

        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        foreach (var def in selectionDefs)
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
            {
                unpaired.Add(def.Id);
                continue;
            }
            comparedIds.Add(def.Id);

            // GATE A.
            bool oldGateA = def.ComboBox?.Options?.Any(o => o.ValueMappings != null) == true;
            bool newGateA = s.States.Any(st => st.Set.Count > 0);
            if (oldGateA != newGateA)
                mismatches.Add($"{def.Id}: GATE A old(ComboBox any ValueMappings)={oldGateA} != new(States any Set)={newGateA}");

            var options = def.ComboBox?.Options;
            if (options == null)
            {
                // A Selection with a null ComboBox is unexpected (system-tray/dns/update-policy all have one); flag it.
                mismatches.Add($"{def.Id}: Selection def has null ComboBox.Options");
                continue;
            }

            // States order must line up 1:1 with ComboBox.Options order for the per-index gate/resolve to be valid.
            if (options.Count != s.States.Count)
            {
                mismatches.Add($"{def.Id}: ComboBox has {options.Count} options but catalog has {s.States.Count} States");
                continue;
            }

            // GATE B (per index).
            for (int i = 0; i < options.Count; i++)
            {
                gateBIndicesCompared++;
                bool oldGateB = options[i].ValueMappings == null;      // old: no mappings -> resolve returns empty
                bool newGateB = s.States[i].Set.Count == 0;            // new: empty Set -> resolve returns empty
                if (oldGateB != newGateB)
                    mismatches.Add($"{def.Id}[{i}]: GATE B old(ValueMappings==null)={oldGateB} != new(Set.Count==0)={newGateB}");
            }
        }

        foreach (var id in unpaired)
            _output.WriteLine($"[unpaired] {id}");
        _output.WriteLine($"{comparedIds.Count} selection settings compared, {gateBIndicesCompared} option indices, {unpaired.Count} unpaired, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.NotEmpty(comparedIds);
        Assert.True(gateBIndicesCompared > 0, "zero option indices compared - population scoping bug");
        // The production swap pairs via SettingCatalog.Find; an unpaired selection would diverge (old warns-and-emits-
        // nothing via ComboBox, new can't resolve), so every shipped selection must be catalog-paired.
        Assert.True(unpaired.Count == 0, "selection settings with NO catalog peer: " + string.Join(", ", unpaired));
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} selection gate mismatches (catalog State.Set vs old ComboBox ValueMappings):\n" + string.Join("\n", mismatches));
    }

    /// <summary>Slice E1c also flips the PowerShell-script pass pairing (FeatureRegistryScriptSection) from the
    /// exact FirstOrDefault to the alias-normalizing SettingCatalog.Find. FirstOrDefault and Find diverge ONLY for
    /// aliased ids (the 6 OS-merged "This PC" -win10 toggles): the OLD script pass routed them to
    /// AppendPowerShellScripts(def) (FirstOrDefault -> null), the NEW one to AppendPowerShellScriptsFromCatalog
    /// (Find -> canonical). That flip is OUTPUT-neutral only because every aliased setting is SCRIPT-LESS on both
    /// sides - both passes emit nothing. Pin that invariant so a future script added to an aliased setting (or a
    /// new -win10-style alias on a script-bearing setting) fails RED and forces the toggle/action emit pairings to
    /// move to Find with their own check.</summary>
    [Fact]
    public void ScriptPassFindFlip_IsNeutral_BecauseAliasedSettingsAreScriptLess()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var aliasedDefs = AllDefinitions().Where(d => SettingIdAliases.Normalize(d.Id) != d.Id).ToList();

        // Vacuity guard: aliased ids (the 6 -win10 This-PC toggles) must actually exist for this to mean anything.
        Assert.NotEmpty(aliasedDefs);

        var violations = new List<string>();
        foreach (var def in aliasedDefs)
        {
            // OLD side: the aliased def's scripts (what AppendPowerShellScripts would emit). Script-less == empty.
            if (def.PowerShellScripts.Count > 0)
                violations.Add($"{def.Id}: aliased OLD def carries {def.PowerShellScripts.Count} PowerShellScript(s) - the Find-flip is no longer script-neutral");

            // NEW side: the canonical catalog Setting's ScriptEffects (what AppendPowerShellScriptsFromCatalog emits).
            var canonicalId = SettingIdAliases.Normalize(def.Id);
            if (catalogById.TryGetValue(canonicalId, out var s) && s.States.Any(st => st.Effects.OfType<ScriptEffect>().Any()))
                violations.Add($"{canonicalId}: aliased catalog setting carries a ScriptEffect - the Find-flip is no longer script-neutral");
        }

        Assert.True(violations.Count == 0,
            "the E1c script-pass Find-flip is neutral ONLY while aliased settings are script-less; a script on an aliased setting needs its own equivalence check before it can ship:\n"
                + string.Join("\n", violations));
    }
}
