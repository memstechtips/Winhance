using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice 7e-5: pins the authored <see cref="Setting.CustomStateScripts"/> (the UN-BAKED custom-state
/// script home the autounattend "Custom" path emits from) BYTE-FOR-BYTE against the old defs. For every def with
/// a ComboBox AND PowerShellScripts - the population whose "Selection with no SelectedIndex" config shape the old
/// emitter served from the raw def scripts - the paired catalog Setting must carry EXACTLY
/// def.PowerShellScripts.Count entries, per-index: Script == the raw EnabledScript (placeholders like
/// <c>{{primary}}</c> INTACT - never an option-baked body) and Run == the def's RunContext.
///
/// WHY THIS CLASS IS LOAD-BEARING (not redundant with CatalogAuthoringEquivalenceTests):
///   - gaming-touch-keyboard-service is in CatalogAuthoringEquivalenceTests.PrecedenceCorrectedIds, so it is
///     EXEMPT from the converter-vs-authored structural gate - this conformance is that setting's ONLY
///     enforcement of CustomStateScripts. The other three (explorer-customization-shortcut-arrow,
///     gaming-dns-server, taskbar-system-tray-icons-11) are double-covered.
///   - The AutounattendScriptBuilderRoutingTests def fixtures went INERT with the 7e-5 routing flip (the custom
///     path reads the catalog, so fixtures carry no PowerShellScripts payload) - the def&lt;-&gt;catalog byte pinning
///     that used to ride on those fixtures' real payloads survives HERE.
///
/// Also pins the complement: every OTHER paired def (no ComboBox or no scripts) has an EMPTY
/// CustomStateScripts - only the script-bearing Selections carry any. Machine-independent: catalog + old defs
/// only, no I/O. Run: dotnet test --filter CustomStateScriptsConformance</summary>
public class CustomStateScriptsConformanceTests
{
    private readonly ITestOutputHelper _output;

    public CustomStateScriptsConformanceTests(ITestOutputHelper output) => _output = output;

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
    public void CustomStateScriptsConformance_CatalogCarriesRawDefScripts_ByteForByte()
    {
        var defs = AllDefinitions().ToList();
        var inScope = defs.Where(d => d.ComboBox != null && d.PowerShellScripts.Count > 0).ToList();
        var inScopeIds = new HashSet<string>(inScope.Select(d => d.Id));

        var mismatches = new List<string>();
        int entriesCompared = 0;

        foreach (var def in inScope)
        {
            var catalog = SettingCatalog.Find(def.Id);
            if (catalog == null)
            {
                mismatches.Add($"{def.Id}: script-bearing selection has NO catalog peer");
                continue;
            }

            if (catalog.CustomStateScripts.Count != def.PowerShellScripts.Count)
            {
                mismatches.Add($"{def.Id}: CustomStateScripts.Count {catalog.CustomStateScripts.Count} != def.PowerShellScripts.Count {def.PowerShellScripts.Count}");
                continue;
            }

            for (int i = 0; i < def.PowerShellScripts.Count; i++)
            {
                entriesCompared++;
                var ps = def.PowerShellScripts[i];
                var cs = catalog.CustomStateScripts[i];
                if (cs.Script != ps.EnabledScript)
                    mismatches.Add($"{def.Id}[{i}]: Script differs from raw def EnabledScript\n--- DEF ---\n{ps.EnabledScript}\n--- CATALOG ---\n{cs.Script}");
                if (cs.Run != ps.RunContext)
                    mismatches.Add($"{def.Id}[{i}]: Run {cs.Run} != def RunContext {ps.RunContext}");
            }
        }

        // Complement guard: only the script-bearing Selections may carry custom-state scripts. Paired ids are
        // canonical (SettingCatalog.Find normalizes aliases), so compare against the scope's canonical ids -
        // none of the scope ids are aliased.
        foreach (var def in defs)
        {
            var catalog = SettingCatalog.Find(def.Id);
            if (catalog != null && catalog.CustomStateScripts.Count > 0 && !inScopeIds.Contains(catalog.Id))
                mismatches.Add($"{catalog.Id} (via def {def.Id}): NOT a ComboBox+scripts def but catalog carries {catalog.CustomStateScripts.Count} CustomStateScripts");
        }

        _output.WriteLine($"script-bearing selections: [{string.Join(", ", inScopeIds.OrderBy(x => x))}], {entriesCompared} entries compared, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // Vacuity guards: the shipped population is exactly these 4 settings / 5 def script entries. A drop to
        // zero (or a silently narrowed scope) must fail loudly, and a NEW script-bearing selection must show up
        // here so its custom-state script home gets authored + covered.
        Assert.Equal(
            new[]
            {
                "explorer-customization-shortcut-arrow",
                "gaming-dns-server",
                "gaming-touch-keyboard-service",
                "taskbar-system-tray-icons-11",
            },
            inScopeIds.OrderBy(x => x).ToArray());
        Assert.Equal(5, inScope.Sum(d => d.PowerShellScripts.Count));

        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} CustomStateScripts conformance mismatches (catalog vs raw def EnabledScript+RunContext):\n"
                + string.Join("\n\n", mismatches));
    }
}
