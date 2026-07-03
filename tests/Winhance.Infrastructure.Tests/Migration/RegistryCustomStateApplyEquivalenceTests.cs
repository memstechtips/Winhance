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
}
