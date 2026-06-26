using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for the DNS-server selection (custom DnsServerDetector detection,
/// new-engine APPLY since Phase 6.4b Slice 6), compares the old live selection apply's PowerShell-script intent
/// (SettingOperationExecutor, mirrored - including the {{primary}}/{{secondary}}/{{dohtemplate}} ScriptVariables
/// substitution) against the new ApplyPlanBuilder plan, for each ComboBox option. The setting has no registry
/// targets, so the comparison is effects-only. Pure - no registry I/O, so the result depends only on the catalog,
/// not the machine. Run: dotnet test --filter DnsServerApplyEquivalence</summary>
public class DnsServerApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public DnsServerApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void DnsServerApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunDnsServerApply(AllDefinitions());

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}");
            if (!row.Match)
            {
                _output.WriteLine($"    old: {row.OldState}");
                _output.WriteLine($"    new: {row.NewState}");
            }
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        Assert.NotEmpty(rows);
        Assert.True(rows.All(r => r.Match), $"{mismatched} DNS-server apply plans differ - see output");
    }
}
