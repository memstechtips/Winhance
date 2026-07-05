using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 Slice E1a precondition for routing the autounattend scheduled-task emission through the new
/// catalog model: the NEW FeatureRegistryScriptSection.CollectScheduledTasksFromCatalog (reads the catalog Setting's
/// TaskTargets + Display.Description) must produce the SAME (TaskPath, /Enable|/Disable, Description) tuples - and
/// therefore the SAME emitted schtasks batch - as the OLD CollectScheduledTasks (reads
/// settingDef.ScheduledTaskSettings + Description), for every scheduled-task setting and both selection states.
///
/// The catalog now carries ONE TaskTarget per scheduled task (gaming-task-windows-ai controls two: RecallConfiguration
/// + RecallPipeline), so the catalog reproduces the old emitter's full task list rather than dropping the extra tasks.
/// This [Fact] feeds the SAME ConfigurationItem (same IsSelected) to BOTH collections and renders each tuple list
/// through the shared AppendScheduledTaskBatch, comparing byte-for-byte, so any divergence is purely in the source
/// (settingDef vs catalog). Green means flipping the scheduled-task call site onto the catalog is provably faithful.
/// Pure - depends only on the catalog, not the machine.
/// Run: dotnet test --filter ScriptGenScheduledTaskEquivalence</summary>
public class ScriptGenScheduledTaskEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenScheduledTaskEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ScriptGenScheduledTaskEquivalence_NewCatalogEmitterMatchesOld()
    {
        var section = new FeatureRegistryScriptSection(
            new RegistryCommandEmitter(new Mock<ILogService>().Object),
            new Mock<ILogService>().Object);

        var taskDefs = AllDefinitions()
            .Where(d => d.ScheduledTaskSettings.Count > 0)
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        int unpaired = 0;

        foreach (var def in taskDefs)
        {
            var catalogSetting = SettingCatalog.Find(def.Id);
            if (catalogSetting == null)
            {
                unpaired++;
                continue;
            }

            foreach (var isSelected in new[] { true, false })
            {
                var configItem = new ConfigurationItem
                {
                    Id = def.Id,
                    IsSelected = isSelected,
                    InputType = def.InputType,
                };

                var oldTuples = section.CollectScheduledTasks(def, configItem).ToList();
                var newTuples = section.CollectScheduledTasksFromCatalog(catalogSetting, configItem).ToList();

                // Render both tuple lists through the shared batch emitter (the actual emitted schtasks script).
                var sbOld = new StringBuilder();
                if (oldTuples.Count > 0)
                    section.AppendScheduledTaskBatch(sbOld, oldTuples, "");

                var sbNew = new StringBuilder();
                if (newTuples.Count > 0)
                    section.AppendScheduledTaskBatch(sbNew, newTuples, "");

                compared.Add($"{def.Id} isSelected={isSelected} ({oldTuples.Count} task(s))");

                if (sbOld.ToString() != sbNew.ToString())
                {
                    mismatches.Add(
                        $"{def.Id} isSelected={isSelected}:\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
                }
            }
        }

        _output.WriteLine($"{compared.Count} (task-setting,isSelected) tuples compared, {mismatches.Count} mismatches, {unpaired} unpaired");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously: scheduled-task settings must be present...
        Assert.NotEmpty(compared);
        // ...and every one must be catalog-paired (else the catalog emitter would silently drop its tasks).
        Assert.Equal(0, unpaired);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} scheduled-task emitter byte mismatches (new catalog CollectScheduledTasksFromCatalog vs old CollectScheduledTasks):\n"
                + string.Join("\n\n", mismatches));
    }
}
