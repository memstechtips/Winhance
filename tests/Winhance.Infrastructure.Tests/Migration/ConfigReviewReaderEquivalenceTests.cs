using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice E5 precondition: ConfigReviewService.BuildComboBoxOptions builds the review combo-box display from
/// the old def's ComboBox options; E5 moves it onto the catalog Setting's States. DisplayName->State.Label,
/// Tooltip->State.Tooltip (Slice B, proven by CatalogTooltipEquivalenceTests), and IsSubjectivePreference->
/// Display.IsSubjectivePreference (a per-setting field the converter copies) are already covered. This proves the
/// remaining per-option flags map to catalog roles IDENTICALLY over the whole selection population, machine-
/// independently: options[i].IsRecommended &lt;=&gt; States[i].HasRole(Recommended) and options[i].IsDefault &lt;=&gt;
/// States[i].HasRole(WindowsDefault). The one shape to watch: powercfg selections author CONTEXT-SCOPED roles
/// (Recommended@AC/DC), so HasRole(kind) (unconditional context) is false for them - which is faithful ONLY if their
/// ComboBox options do not set the IsRecommended/IsDefault flag (they derive recommended from per-mode VALUES). This
/// test catches any divergence. Run: dotnet test --filter ConfigReviewReaderEquivalence</summary>
public class ConfigReviewReaderEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ConfigReviewReaderEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void CatalogStateRoles_MatchOldComboBoxOptionFlags_PerIndex()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var divergentIds = new HashSet<string>();
        var compared = 0;
        var recommendedCount = 0;
        var defaultCount = 0;

        foreach (var def in AllDefinitions())
        {
            // BuildComboBoxOptions handles NON-power-plan selections (power-plan is a separate branch).
            if (def.InputType != InputType.Selection || def.Id == SettingIds.PowerPlanSelection)
                continue;
            var options = def.ComboBox?.Options;
            if (options == null)
                continue;
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;
            if (options.Count != s.States.Count)
            {
                mismatches.Add($"{def.Id}: ComboBox has {options.Count} options but catalog has {s.States.Count} States");
                continue;
            }
            compared++;

            for (int i = 0; i < options.Count; i++)
            {
                bool oldRec = options[i].IsRecommended;
                bool newRec = s.States[i].HasRole(RoleKind.Recommended);
                if (oldRec) recommendedCount++;
                if (oldRec != newRec)
                {
                    mismatches.Add($"{def.Id}[{i}]: old IsRecommended={oldRec} != catalog HasRole(Recommended)={newRec}");
                    divergentIds.Add(def.Id);
                }

                bool oldDef = options[i].IsDefault;
                bool newDef = s.States[i].HasRole(RoleKind.WindowsDefault);
                if (oldDef) defaultCount++;
                if (oldDef != newDef)
                {
                    mismatches.Add($"{def.Id}[{i}]: old IsDefault={oldDef} != catalog HasRole(WindowsDefault)={newDef}");
                    divergentIds.Add(def.Id);
                }
            }
        }

        _output.WriteLine($"{compared} selection settings compared; {recommendedCount} recommended, {defaultCount} default options; {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 50, $"only compared {compared} selection settings - population scoping bug");
        Assert.True(recommendedCount > 0 && defaultCount > 0, $"a flag bucket was empty (rec={recommendedCount}, def={defaultCount}) - the comparison would be vacuous");
        // The 2 DETECTOR selections (system-tray, dns) formerly diverged here: ConvertSystemTray/ConvertDnsServer
        // dropped the option IsRecommended/IsDefault -> StateRole mapping that ConvertSelection carries. That gap is
        // now CLOSED (both converter paths + the 2 authored catalogs carry the roles via the shared RolesForOption
        // helper), so the whole selection population matches per index with ZERO divergence. A new divergence fails RED.
        Assert.True(divergentIds.Count == 0,
            $"role-flag divergence (expected none - the detector-selection role gap is closed): "
                + $"[{string.Join(", ", divergentIds.OrderBy(x => x))}]:\n" + string.Join("\n", mismatches));
    }
}
