using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for every PLAIN registry selection (no per-option scripts), compares the old
/// live custom-state apply intent (SettingOperationExecutor's CustomStateValues branch, per RegistrySetting whose
/// ValueName is in the dict) against the new ApplyPlanBuilder.BuildRegistryCustomState plan, using each option's
/// ValueMappings as a representative per-ValueName custom dict. Pure - no registry I/O, so the result depends only on
/// the catalog. Locks the precondition for the resolver routing CustomStateValues through the new engine.
/// Run: dotnet test --filter RegistryCustomStateApplyEquivalence</summary>
public class RegistryCustomStateApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public RegistryCustomStateApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void RegistryCustomStateApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunRegistryCustomStateApply(AllDefinitions());

        foreach (var row in rows.Where(r => !r.Match))
        {
            _output.WriteLine($"[DIFF] {row.Id}");
            _output.WriteLine($"    old: {row.OldState}");
            _output.WriteLine($"    new: {row.NewState}");
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        Assert.NotEmpty(rows);
        Assert.True(rows.All(r => r.Match), $"{mismatched} registry custom-state apply plans differ - see output");
    }

    [Fact]
    public void ScriptBearing_registry_selections_produce_state_effects_so_the_resolver_gate_excludes_them()
    {
        // The resolver routes CustomStateValues on !States.Any(Effects); this harness excludes script selections on
        // PowerShellScripts.Count > 0. Those two predicates agree ONLY IF every script-bearing registry selection
        // converts to >=1 state effect (so the resolver's effects-gate excludes it, matching this harness). Lock the
        // invariant: a future plain registry selection with a PowerShellScripts body but all-None options would
        // convert to NO state effects, be routed by the resolver, and silently DROP the script (the old executor
        // runs it via useEnabled=enable for a dict value). This [Fact] fails if such a setting is ever added.
        var offenders = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection
                        && d.RegistrySettings.Count > 0
                        && d.PowerShellScripts.Count > 0
                        && !SettingDefinitionConverter.ConvertSelection(d).States.Any(st => st.Effects.Count > 0))
            .Select(d => d.Id)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Script-bearing registry selections that convert to NO state effects (resolver would route + drop the "
            + "script): " + string.Join(", ", offenders));
    }
}
