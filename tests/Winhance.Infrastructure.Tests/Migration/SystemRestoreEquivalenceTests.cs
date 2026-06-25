using System.Collections.Generic;
using System.Linq;
using Moq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check, run on Windows: for the system-restore toggle, compares the app's real
/// detection (ISystemRestoreService.IsEnabledForC) against the new SystemRestoreDetector. Green when they
/// agree. Run: dotnet test --filter SystemRestoreEquivalence</summary>
public class SystemRestoreEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public SystemRestoreEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every SettingDefinition the app ships, pulled straight from the static feature providers
    /// (no DI, no Windows-version filtering - so the comparison population is the full raw catalog).</summary>
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
    public void SystemRestoreEquivalence_OldAndNewDetectionAgree()
    {
        var log = new Mock<ILogService>();
        var restoreService = new SystemRestoreService(log.Object);

        var rows = RegistryToggleEquivalenceHarness.RunSystemRestore(restoreService, AllDefinitions());

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}: old={row.OldState} new={row.NewState}");
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        if (mismatched > 0)
        {
            _output.WriteLine("Mismatches:");
            foreach (var row in rows.Where(r => !r.Match))
                _output.WriteLine($"  {row.Id}: old={row.OldState} new={row.NewState}");
        }

        Assert.True(rows.All(r => r.Match), $"{mismatched} system-restore settings differ - see output");
    }
}
