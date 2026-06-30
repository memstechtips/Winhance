using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 F2b precondition for swapping the autounattend toggle emitter onto the new catalog model: the
/// NEW RegistryCommandEmitter.AppendToggleCommandsFromCatalog (reads the catalog Setting's active SettingState /
/// RegTargets / Display.Description) must emit the SAME SET of PowerShell commands (multiset, not byte order - see
/// NormalizeLines) as the OLD RegistryCommandEmitter.AppendToggleCommandsFiltered (reads
/// SettingDefinition.RegistrySettings' EnabledValue/DisabledValue / Description) for every NON-build-gated toggle the
/// script emitter routes through it (build-gated OS-merged toggles stay on the old emitter - see the in-loop comment).
///
/// In FeatureRegistryScriptSection both InputType.Toggle (line 120) and InputType.Action (line 137, when IsSelected)
/// call AppendToggleCommandsFiltered. This [Fact] covers ONLY InputType.Toggle: the catalog deliberately models the
/// three Action settings (taskbar-clean, start-menu-clean-10/-11) as Effects (RegistryWriteEffect/ScriptEffect with
/// no States and no Targets), so AppendToggleCommandsFromCatalog - which keys off the active "Enabled"/"Disabled"
/// SettingState - emits nothing for them, whereas the old method emits their RegistrySettings writes. Actions
/// therefore cannot be flipped onto AppendToggleCommandsFromCatalog; their registry emission is a separate
/// Effects-based concern for F2c, not this toggle-mirror method.
///
/// The OLD method also emits the RegContents tail (AppendRegContentCommands) which the NEW method intentionally does
/// NOT - so this [Fact] is further restricted to RegContents-free toggles, where the OLD method emits only registry
/// commands and full byte equality is meaningful. RegContent-bearing toggles are covered by F2c (the call-site flip).
/// The SAME ConfigurationItem (the same IsSelected, NO CustomStateValues) is fed to BOTH methods, for BOTH selection
/// states and BOTH hive passes, so any divergence is purely in the emitters. Green means flipping the toggle call
/// site onto the new model is provably faithful. Pure - depends only on the catalog, not the machine.
/// Run: dotnet test --filter ScriptGenToggleEquivalence</summary>
public class ScriptGenToggleEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenToggleEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ScriptGenToggleEquivalence_NewCatalogEmitterMatchesOldByteForByte()
    {
        // One emitter instance, reused; the ILogService mock is never exercised on these paths.
        var emitter = new RegistryCommandEmitter(new Mock<ILogService>().Object);

        // InputType.Toggle only. InputType.Action also routes through AppendToggleCommandsFiltered
        // (FeatureRegistryScriptSection line 137), but the catalog models Action writes as Effects, not
        // States/RegTargets, so AppendToggleCommandsFromCatalog emits nothing for them - see the class summary.
        // Restrict to RegContents-free toggles so the OLD method's RegContents tail (which the NEW method does not
        // emit) cannot make the byte comparison diverge; RegContent-bearing toggles are covered by the F2c flip.
        var toggleDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Toggle
                     && (d.RegContents == null || d.RegContents.Count == 0))
            .ToList();

        var mismatches = new List<string>();
        var compared = new List<string>();
        int unpaired = 0;
        int buildGated = 0;

        foreach (var def in toggleDefs)
        {
            var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (catalogSetting == null)
            {
                unpaired++;
                continue;
            }

            // Build-gated settings (the 6 OS-merged "This PC folder" toggles carry per-target AppliesTo Win10/Win11
            // ranges) stay on the OLD emitter in the F2b flip: AppendToggleCommandsFromCatalog has no build context, so
            // it would emit BOTH OS variants' targets while the OLD OS-filtered def carries only one. Skip them here
            // (the flip routes them to AppendToggleCommandsFiltered); fully retiring SettingDefinition for them needs the
            // build threaded into the emitter - a later slice.
            if (catalogSetting.Targets.Any(t => t.AppliesTo.Count > 0))
            {
                buildGated++;
                continue;
            }

            foreach (var isSelected in new[] { true, false })
            {
                foreach (var isHkcu in new[] { false, true })
                {
                    var configItem = new ConfigurationItem
                    {
                        Id = def.Id,
                        IsSelected = isSelected,
                        InputType = def.InputType,
                    };

                    var sbOld = new StringBuilder();
                    emitter.AppendToggleCommandsFiltered(sbOld, def, configItem, isHkcu, "");

                    var sbNew = new StringBuilder();
                    emitter.AppendToggleCommandsFromCatalog(sbNew, catalogSetting, def, configItem, isHkcu, "");

                    compared.Add($"{def.Id} isSelected={isSelected} isHkcu={isHkcu}");

                    var oldText = sbOld.ToString();
                    var newText = sbNew.ToString();

                    // Order-sensitive emissions are compared BYTE-EXACT; order-independent ones by MULTISET.
                    // Multiset rationale: the catalog groups a mirror (same ValueName under several KeyPaths) into ONE
                    // RegTarget with several Paths, so the new emitter writes a mirror's paths consecutively while the
                    // old emitter interleaves them in authored RegistrySetting order (e.g. privacy-diagnostics). Those
                    // are INDEPENDENT registry writes, so the reorder is semantically identical and sorting the lines
                    // proves the same commands while still catching any extra/missing/changed command (a build-gated
                    // target leaking in was caught this way). EXCEPTION: a Remove-RegistryKey recursively deletes the
                    // value writes around it (e.g. explorer-context-menu-dism's trailing self-delete), so its RELATIVE
                    // ORDER is load-bearing - those emissions must be byte-exact, both to be faithful today and to catch
                    // a FUTURE catalog target-reorder that the multiset would silently pass.
                    bool orderSensitive = oldText.Contains("Remove-RegistryKey") || newText.Contains("Remove-RegistryKey");
                    bool equal = orderSensitive
                        ? oldText == newText
                        : NormalizeLines(oldText).SequenceEqual(NormalizeLines(newText));
                    if (!equal)
                    {
                        mismatches.Add(
                            $"{def.Id} isSelected={isSelected} isHkcu={isHkcu} (orderSensitive={orderSensitive}):\n--- OLD ---\n{sbOld}\n--- NEW ---\n{sbNew}");
                    }
                }
            }
        }

        _output.WriteLine($"{compared.Count} (setting,isSelected,hive) tuples compared, {mismatches.Count} mismatches, {unpaired} unpaired skipped, {buildGated} build-gated skipped");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously.
        Assert.NotEmpty(compared);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} toggle byte mismatches (new catalog emitter vs old AppendToggleCommandsFiltered):\n"
                + string.Join("\n\n", mismatches));
    }

    /// <summary>Non-empty trimmed command lines, sorted ordinally - a multiset view so a benign mirror-path reorder
    /// (the catalog groups a mirror's paths; the old emitter interleaves them) compares equal while any
    /// extra/missing/changed command still differs.</summary>
    private static List<string> NormalizeLines(string text) =>
        text.Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.Length > 0)
            .OrderBy(l => l, System.StringComparer.Ordinal)
            .ToList();
}
