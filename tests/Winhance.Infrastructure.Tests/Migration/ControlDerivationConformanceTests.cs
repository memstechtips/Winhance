using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Proves the DERIVED <see cref="Setting.Control"/> equals the render-kind implied by each setting's old
/// InputType, for every catalog-paired setting - the machine-independent guard that the shape-derivation matches
/// authorial intent (the old InputType is the ground truth). Replaces a stored-field authoring gate and survives the
/// converter teardown as a permanent conformance check.</summary>
public class ControlDerivationConformanceTests
{
    private static IEnumerable<SettingDefinition> AllOldDefs() =>
        ExplorerCustomizations.GetExplorerCustomizations().Settings
        .Concat(StartMenuCustomizations.GetStartMenuCustomizations().Settings)
        .Concat(TaskbarCustomizations.GetTaskbarCustomizations().Settings)
        .Concat(WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings)
        .Concat(PowerOptimizations.GetPowerOptimizations().Settings)
        .Concat(GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings)
        .Concat(NotificationOptimizations.GetNotificationOptimizations().Settings)
        .Concat(PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings)
        .Concat(SoundOptimizations.GetSoundOptimizations().Settings)
        .Concat(UpdateOptimizations.GetUpdateOptimizations().Settings);

    [Fact]
    public void Derived_Control_matches_old_InputType_for_every_paired_setting()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        foreach (var def in AllOldDefs())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue; // unpaired def (no catalog peer) - out of scope for this gate
            compared++;
            var expected = s.OptionSource is not null
                ? ControlKind.PowerPlan
                : def.InputType switch
                {
                    InputType.NumericRange => ControlKind.Slider,
                    InputType.Action => ControlKind.Action,
                    InputType.Selection => ControlKind.Selection,
                    _ => ControlKind.Toggle, // Toggle (+ CheckBox, which no setting uses)
                };
            if (s.Control != expected)
                mismatches.Add($"{def.Id}: derived {s.Control} != expected {expected} (old InputType {def.InputType})");
        }
        Assert.True(compared > 300, $"only compared {compared} settings - population scoping bug");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} Control mismatches:\n" + string.Join("\n", mismatches));
    }
}
