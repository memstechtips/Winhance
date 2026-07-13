using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice E2 precondition: ConfigurationApplicationBridgeService's config-import READS are moving off the old
/// SettingDefinition onto the catalog Setting (paired via SettingCatalog.Find). This proves the three swapped reads
/// are old-vs-new IDENTICAL over the whole shipped population, machine-independently (catalog + old defs only, no I/O):
///
///   1. WAVE DEPENDENCIES (BuildDependencyWaves): the config-import dependency-ordering set moves from
///      def.Dependencies.Where(type != RequiresValueBeforeAnyChange).Select(RequiredSettingId) to the catalog
///      States' Links (LinkKind.Requires OtherIds). Faithful iff the two SETS are equal per setting - the converter
///      maps RequiresEnabled/RequiresDisabled (BuildLinks) + RequiresSpecificValue (BuildValuePrereqLinks) to
///      Requires-Links and drops RequiresValueBeforeAnyChange, exactly the old filter. The one edge this catches:
///      a RequiresSpecificValue dep with a NULL RequiredValue is in the OLD set but BuildValuePrereqLinks skips it
///      (it needs RequiredValue is {} v), so it would be DROPPED from the catalog - a real divergence if any exists.
///
///   2. RequiresConfirmation (ApplySettingItemAsync): def.RequiresConfirmation moves to catalog.Apply.RequiresConfirmation.
///      BuildApply copies the flag (and its None early-return is also RequiresConfirmation == false), so they must be equal.
///
///   3. InputType dispatch (ApplySettingItemAsync): def.InputType == Selection/NumericRange/Action moves to catalog
///      Control. Unlike the E1c script-gen dispatch, the bridge does NOT skip power-plan-selection, so InputType ==
///      Selection maps to Control in {Selection, PowerPlan} (the power-plan is InputType.Selection + OptionSource ->
///      Control.PowerPlan). NumericRange -> Slider, Action -> Action.
///
///   4. Numeric isPowerCfg gate (ResolveNumericRangeValue, Slice 6): def.PowerCfgSettings?.Any() moves to
///      catalog Targets.OfType<PowerCfgTarget>().Any() - the gate that decides system->display unit conversion.
///      (The units themselves are proven by PowerCfgHelperCatalogEquivalenceTests.)
///
/// Green means the E2 reader swaps are provably behaviour-preserving. Pure - depends only on the catalog.
/// Run: dotnet test --filter ConfigBridgeReaderEquivalence</summary>
public class ConfigBridgeReaderEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ConfigBridgeReaderEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    /// <summary>WAVE-DEPENDENCY swap: the old config-import dependency set (every RequiredSettingId whose dep type is
    /// not RequiresValueBeforeAnyChange) equals the catalog's aggregated Requires-Link OtherIds. Compared as SETS
    /// (wave readiness only needs set membership; duplicates and order are irrelevant to All(processed.Contains)).</summary>
    [Fact]
    public void WaveDependencies_CatalogRequiresLinks_MatchOldDependencies()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var withDeps = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue; // unpaired def - the production read falls back to def.Dependencies for these
            compared++;

            // OLD: the exact expression BuildDependencyWaves uses.
            var oldSet = new HashSet<string>(
                (def.Dependencies ?? new List<SettingDependency>())
                    .Where(d => d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange)
                    .Select(d => d.RequiredSettingId));

            // NEW: the catalog Requires-Links aggregated across states (the canonical CatalogValidator/RelationshipResolver
            // aggregation), OtherId set.
            var newSet = new HashSet<string>(
                s.States.SelectMany(st => st.Links)
                    .Where(l => l.Kind == LinkKind.Requires)
                    .Select(l => l.OtherId));

            if (oldSet.Count > 0)
                withDeps++;

            if (!oldSet.SetEquals(newSet))
                mismatches.Add($"{def.Id}: wave-dep set differs old=[{string.Join(",", oldSet.OrderBy(x => x))}] new=[{string.Join(",", newSet.OrderBy(x => x))}]");
        }

        _output.WriteLine($"{compared} settings compared, {withDeps} with dependencies, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 300, $"only compared {compared} settings - population scoping bug");
        Assert.True(withDeps > 0, "no setting had any wave dependency - the comparison would be vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} wave-dependency mismatches (catalog Requires-Links vs old Dependencies):\n" + string.Join("\n", mismatches));
    }

    /// <summary>RequiresConfirmation swap: def.RequiresConfirmation == catalog.Apply.RequiresConfirmation for every paired setting.</summary>
    [Fact]
    public void RequiresConfirmation_CatalogApply_MatchesOldFlag()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var confirmCount = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;
            compared++;
            if (def.RequiresConfirmation)
                confirmCount++;
            if (def.RequiresConfirmation != s.Apply.RequiresConfirmation)
                mismatches.Add($"{def.Id}: old RequiresConfirmation={def.RequiresConfirmation} != catalog Apply.RequiresConfirmation={s.Apply.RequiresConfirmation}");
        }

        _output.WriteLine($"{compared} settings compared, {confirmCount} require confirmation, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 300, $"only compared {compared} settings - population scoping bug");
        Assert.True(confirmCount > 0, "no setting required confirmation - the comparison would be vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} RequiresConfirmation mismatches:\n" + string.Join("\n", mismatches));
    }

    /// <summary>InputType-dispatch swap: over every paired setting, the bridge's three InputType checks map to catalog
    /// Control - Selection to {Selection, PowerPlan} (power-plan-selection is NOT skipped here), NumericRange to Slider,
    /// Action to Action. Bidirectional per bucket.</summary>
    [Fact]
    public void InputTypeDispatch_CatalogControl_MatchesOldInputType()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var selection = 0;
        var numeric = 0;
        var action = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;
            compared++;

            bool oldSelection = def.InputType == InputType.Selection;
            bool newSelection = s.Control is ControlKind.Selection or ControlKind.PowerPlan;
            if (oldSelection) selection++;
            if (oldSelection != newSelection)
                mismatches.Add($"{def.Id}: Selection old={oldSelection} != new(Control in Selection/PowerPlan)={newSelection} (InputType={def.InputType}, Control={s.Control})");

            bool oldNumeric = def.InputType == InputType.NumericRange;
            bool newNumeric = s.Control == ControlKind.Slider;
            if (oldNumeric) numeric++;
            if (oldNumeric != newNumeric)
                mismatches.Add($"{def.Id}: NumericRange old={oldNumeric} != new(Control.Slider)={newNumeric} (InputType={def.InputType}, Control={s.Control})");

            bool oldAction = def.InputType == InputType.Action;
            bool newAction = s.Control == ControlKind.Action;
            if (oldAction) action++;
            if (oldAction != newAction)
                mismatches.Add($"{def.Id}: Action old={oldAction} != new(Control.Action)={newAction} (InputType={def.InputType}, Control={s.Control})");
        }

        _output.WriteLine($"{compared} settings compared - selection={selection}, numeric={numeric}, action={action}; {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 300, $"only compared {compared} settings - population scoping bug");
        Assert.True(selection > 0 && numeric > 0 && action > 0, $"a dispatch bucket was empty (sel={selection}, num={numeric}, act={action}) - the comparison would be vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} InputType-dispatch mismatches:\n" + string.Join("\n", mismatches));
    }

    /// <summary>Numeric isPowerCfg-gate swap (ResolveNumericRangeValue, Slice 6): the bridge's "is this a
    /// powercfg-backed numeric" gate moves from def.PowerCfgSettings?.Any() to catalog
    /// Targets.OfType&lt;PowerCfgTarget&gt;().Any(). Compared over every paired NumericRange def - the only
    /// population ResolveNumericRangeValue serves (the Slider dispatch fact above proves the populations
    /// coincide). The guarded units value (GetPowerCfgDisplayUnits def vs catalog) is proven by
    /// PowerCfgHelperCatalogEquivalenceTests.</summary>
    [Fact]
    public void NumericIsPowerCfgGate_CatalogPowerCfgTargets_MatchOldPowerCfgSettings()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var powerCfgTrue = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.InputType != InputType.NumericRange)
                continue;
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;
            compared++;

            bool oldGate = def.PowerCfgSettings?.Any() == true;
            bool newGate = s.Targets.OfType<PowerCfgTarget>().Any();
            if (oldGate) powerCfgTrue++;
            if (oldGate != newGate)
                mismatches.Add($"{def.Id}: isPowerCfg old(PowerCfgSettings.Any)={oldGate} != new(PowerCfgTarget.Any)={newGate}");
        }

        _output.WriteLine($"{compared} NumericRange settings compared, {powerCfgTrue} powercfg-backed, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared >= 10, $"only compared {compared} NumericRange settings - population scoping bug");
        Assert.True(powerCfgTrue > 0, "no NumericRange setting was powercfg-backed - the gate comparison would be vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} isPowerCfg-gate mismatches:\n" + string.Join("\n", mismatches));
    }
}
