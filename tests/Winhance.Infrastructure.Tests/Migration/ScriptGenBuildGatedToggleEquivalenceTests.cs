using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 script-gen tail precondition for routing the BUILD-GATED toggles (the 6 OS-merged "This PC
/// folder" settings) through the new catalog emitter. These settings are ONE canonical catalog Setting carrying two
/// per-OS RegTargets (a Win11 HiddenByDefault DWord target + a Win10 KeyExists key-existence target, each gated by
/// Target.AppliesTo). The OLD model splits them into a Win11-only canonical SettingDefinition and a Win10-only
/// "-win10" sibling; at load exactly one survives per machine (the version filter), so the OLD autounattend emitter
/// emits the OS-appropriate def. The new emitter reproduces that by filtering Targets by the live build.
///
/// This is the N->1 context-adaptive merge verified by PROJECTION: for each canonical setting, project the merged
/// catalog Setting onto each OS by threading that OS's build into AppendToggleCommandsFromCatalog (which drops the
/// targets whose AppliesTo excludes the build), and compare to the OLD emitter run over the corresponding OS-specific
/// def (the Win11 canonical def for a Win11 build, the "-win10" sibling for a Win10 build). The SAME ConfigurationItem
/// (same IsSelected, NO CustomStateValues) feeds BOTH, for BOTH selection states and BOTH hive passes. Comparison
/// mirrors ScriptGenToggleEquivalenceTests: byte-exact when a Remove-RegistryKey is present (recursive-delete order is
/// load-bearing), multiset otherwise (a mirror's paths are independent writes). Green means threading the live build
/// and routing these 6 to the new emitter is provably faithful per OS. Pure - depends only on the catalog.
/// Run: dotnet test --filter ScriptGenBuildGatedToggleEquivalence</summary>
public class ScriptGenBuildGatedToggleEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenBuildGatedToggleEquivalenceTests(ITestOutputHelper output) => _output = output;

    // Representative builds firmly inside each range (Windows 11 starts at build 22000).
    private static readonly WinBuild Win11Build = new(22631);
    private static readonly WinBuild Win10Build = new(19045);

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
    public void ScriptGenBuildGatedToggleEquivalence_PerOsProjectionMatchesOldOsFilteredDef()
    {
        var emitter = new RegistryCommandEmitter(new Mock<ILogService>().Object);
        var allDefs = AllDefinitions().ToList();

        var toggleDefs = allDefs
            .Where(d => d.InputType == InputType.Toggle
                     && (d.RegContents == null || d.RegContents.Count == 0))
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        int canonicalSettings = 0;
        int win10Projections = 0;

        foreach (var canonicalDef in toggleDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == canonicalDef.Id);
            if (catalogSetting == null)
                continue; // unpaired - incl. the "-win10" ids, which are merged away (not in the catalog)
            if (!catalogSetting.Targets.Any(t => t.AppliesTo.Count > 0))
                continue; // not build-gated - covered by ScriptGenToggleEquivalenceTests

            canonicalSettings++;

            // Project onto each OS: the Win11 canonical def vs a Win11 build, the "-win10" sibling vs a Win10 build.
            var win10Def = allDefs.FirstOrDefault(d => d.Id == canonicalDef.Id + "-win10");
            var projections = new List<(string Os, SettingDefinition OldDef, WinBuild Build)>
            {
                ("win11", canonicalDef, Win11Build),
            };
            if (win10Def != null)
            {
                projections.Add(("win10", win10Def, Win10Build));
                win10Projections++;
            }

            foreach (var (os, oldDef, build) in projections)
            {
                foreach (var isSelected in new[] { true, false })
                {
                    foreach (var isHkcu in new[] { false, true })
                    {
                        var configItem = new ConfigurationItem
                        {
                            Id = canonicalDef.Id,
                            IsSelected = isSelected,
                            InputType = InputType.Toggle,
                        };

                        // OLD: the OS-filtered def the version filter would leave on this machine.
                        var sbOld = new StringBuilder();
                        emitter.AppendToggleCommandsFiltered(sbOld, oldDef, configItem, isHkcu, "");

                        // NEW: the merged catalog Setting projected onto this OS via the threaded build.
                        var sbNew = new StringBuilder();
                        emitter.AppendToggleCommandsFromCatalog(sbNew, catalogSetting, configItem, isHkcu, "", build);

                        compared.Add($"{canonicalDef.Id} os={os} isSelected={isSelected} isHkcu={isHkcu}");

                        var oldText = sbOld.ToString();
                        var newText = sbNew.ToString();
                        bool orderSensitive = oldText.Contains("Remove-RegistryKey") || newText.Contains("Remove-RegistryKey");
                        bool equal = orderSensitive
                            ? oldText == newText
                            : NormalizeLines(oldText).SequenceEqual(NormalizeLines(newText));
                        if (!equal)
                        {
                            mismatches.Add(
                                $"{canonicalDef.Id} os={os} isSelected={isSelected} isHkcu={isHkcu} (orderSensitive={orderSensitive}):\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
                        }
                    }
                }
            }
        }

        _output.WriteLine($"{canonicalSettings} build-gated canonical settings ({win10Projections} with a -win10 sibling), {compared.Count} projections compared, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        Assert.NotEmpty(compared);
        // The 6 "This PC folder" toggles are the known build-gated population; each must have a -win10 sibling so the
        // Win10 projection (the divergence-prone key-existence path) is actually exercised, not silently skipped.
        Assert.True(canonicalSettings >= 6, $"Expected at least the 6 'This PC folder' build-gated toggles; found {canonicalSettings}.");
        Assert.Equal(canonicalSettings, win10Projections);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} build-gated toggle projection mismatches (new catalog emitter w/ build vs old OS-filtered def):\n"
                + string.Join("\n\n", mismatches));
    }

    /// <summary>Non-empty trimmed command lines, sorted ordinally - a multiset view so a benign mirror-path reorder
    /// compares equal while any extra/missing/changed command still differs.</summary>
    private static List<string> NormalizeLines(string text) =>
        text.Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.Length > 0)
            .OrderBy(l => l, System.StringComparer.Ordinal)
            .ToList();
}
