using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>Throwaway migration check, run on Windows: for every pure scheduled-task toggle in the catalog,
/// compares the app's real detection (ScheduledTaskService.IsTaskEnabledAsync) against the new engine's,
/// reading the live Task Scheduler. A task that does not exist is "Unavailable" on both sides. Green when
/// every task agrees. Run: dotnet test --filter ScheduledTaskEquivalence</summary>
public class ScheduledTaskEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScheduledTaskEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public async Task ScheduledTaskEquivalence_OldAndNewDetectionAgree()
    {
        // Real scheduled-task service reading the live Task Scheduler; IsTaskEnabledAsync uses only the COM
        // task service + the log, so the file-system dependency is a no-op mock.
        var log = new Mock<ILogService>();
        var fileSystem = new Mock<IFileSystemService>();
        var taskService = new ScheduledTaskService(log.Object, fileSystem.Object);

        var taskDefs = AllDefinitions()
            .Where(RegistryToggleEquivalenceHarness.IsPureScheduledTaskToggle)
            .ToList();

        var rows = await RegistryToggleEquivalenceHarness.RunScheduledTasks(taskService, taskDefs);

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

        Assert.True(rows.All(r => r.Match), $"{mismatched} scheduled-task settings differ - see output");
    }
}
