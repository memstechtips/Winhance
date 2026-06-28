using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

public class CatalogDetectionStateOverlayTests
{
    private static SettingDefinition Toggle() =>
        new() { Id = "t", Name = "t", Description = "t", InputType = InputType.Toggle };

    private static SettingDefinition Selection(params string[] options) => new()
    {
        Id = "s",
        Name = "s",
        Description = "s",
        InputType = InputType.Selection,
        ComboBox = new ComboBoxMetadata
        {
            Options = options.Select(o => new ComboBoxOption { DisplayName = o }).ToList(),
        },
    };

    private static SettingDefinition Bare(InputType type) =>
        new() { Id = "x", Name = "x", Description = "x", InputType = type };

    [Fact]
    public void Toggle_new_disabled_overlays_isenabled_false_and_keeps_old_aux()
    {
        var old = new SettingStateResult { IsEnabled = true, Success = true, ErrorMessage = "keep me" };
        var result = CatalogDetectionStateOverlay.Apply(Toggle(), old,
            new CatalogDetectionResult { StateLabel = "Disabled", Detected = true });
        Assert.False(result.IsEnabled);
        Assert.True(result.Success);              // old auxiliary fields preserved
        Assert.Equal("keep me", result.ErrorMessage);
    }

    [Fact]
    public void Toggle_new_enabled_overlays_isenabled_true()
    {
        var old = new SettingStateResult { IsEnabled = false, Success = true };
        var result = CatalogDetectionStateOverlay.Apply(Toggle(), old,
            new CatalogDetectionResult { StateLabel = "Enabled", Detected = true });
        Assert.True(result.IsEnabled);
    }

    [Fact]
    public void Toggle_custom_or_null_label_keeps_old()
    {
        var old = new SettingStateResult { IsEnabled = true, Success = true };
        Assert.True(CatalogDetectionStateOverlay.Apply(Toggle(), old, new CatalogDetectionResult { StateLabel = null }).IsEnabled);
        Assert.True(CatalogDetectionStateOverlay.Apply(Toggle(), old, new CatalogDetectionResult { StateLabel = "Custom" }).IsEnabled);
    }

    [Fact]
    public void Selection_resolves_label_to_index()
    {
        var old = new SettingStateResult { CurrentValue = 0, Success = true };
        var def = Selection("Off", "On", "Custom mode");
        var result = CatalogDetectionStateOverlay.Apply(def, old,
            new CatalogDetectionResult { StateLabel = "Custom mode", Detected = true });
        Assert.Equal(2, result.CurrentValue);
    }

    [Fact]
    public void Selection_unmatched_label_keeps_old_index()
    {
        var old = new SettingStateResult { CurrentValue = 1, Success = true };
        var def = Selection("Off", "On");
        var result = CatalogDetectionStateOverlay.Apply(def, old,
            new CatalogDetectionResult { StateLabel = "Nonexistent" });
        Assert.Equal(1, result.CurrentValue);
    }

    [Fact]
    public void Numeric_action_and_unpaired_keep_old()
    {
        var old = new SettingStateResult { CurrentValue = 42, IsEnabled = true, Success = true };
        Assert.Equal(42, CatalogDetectionStateOverlay.Apply(
            Bare(InputType.NumericRange), old, new CatalogDetectionResult { Value = 7 }).CurrentValue);
        Assert.Equal(42, CatalogDetectionStateOverlay.Apply(
            Bare(InputType.Action), old, new CatalogDetectionResult { StateLabel = "Enabled" }).CurrentValue);
        // unpaired (null new result) -> old unchanged
        var unpaired = CatalogDetectionStateOverlay.Apply(Toggle(), old, null);
        Assert.True(unpaired.IsEnabled);
        Assert.Equal(42, unpaired.CurrentValue);
    }

    [Fact]
    public void Powercfg_acdc_values_overlay_into_rawvalues_preserving_other_aux()
    {
        var old = new SettingStateResult
        {
            CurrentValue = 42,
            Success = true,
            RawValues = new Dictionary<string, object?> { ["PowerCfgValue"] = 99, ["ACValue"] = 1, ["DCValue"] = 2 },
        };
        var result = CatalogDetectionStateOverlay.Apply(
            Bare(InputType.NumericRange), old,
            new CatalogDetectionResult { Value = 5, AcValue = 5, DcValue = 8, Detected = true });
        Assert.Equal(5, result.RawValues!["ACValue"]);          // new engine's AC overlaid
        Assert.Equal(8, result.RawValues!["DCValue"]);          // new engine's DC overlaid
        Assert.Equal(99, result.RawValues!["PowerCfgValue"]);   // other auxiliary entries preserved
    }

    [Fact]
    public void Null_acdc_leaves_rawvalues_reference_untouched()
    {
        var raw = new Dictionary<string, object?> { ["ACValue"] = 1 };
        var old = new SettingStateResult { IsEnabled = false, Success = true, RawValues = raw };
        var result = CatalogDetectionStateOverlay.Apply(
            Toggle(), old, new CatalogDetectionResult { StateLabel = "Enabled", AcValue = null, DcValue = null });
        Assert.Same(raw, result.RawValues);   // non-powercfg: threading skipped, no clone
        Assert.True(result.IsEnabled);
    }

    [Fact]
    public void Dynamic_options_result_threads_options_and_selection_keeping_old_currentvalue()
    {
        var options = new[]
        {
            new DynamicOption("PowerPlan_Balanced_Name", "381b4222-f694-41f0-9685-ff5bb260df2e"),
            new DynamicOption("PowerPlan_HighPerformance_Name", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", ExistsOnSystem: false),
        };
        var old = new SettingStateResult { Success = true, CurrentValue = 0 };
        var result = CatalogDetectionStateOverlay.Apply(
            Selection("a", "b"), old,
            new CatalogDetectionResult { StateLabel = "381b4222-f694-41f0-9685-ff5bb260df2e", Detected = true, Options = options });

        Assert.Equal(options, result.DynamicOptions);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", result.DynamicSelection);
        Assert.Equal(0, result.CurrentValue);   // additive: the GUID label matches no option DisplayName, old index stays
        Assert.True(result.Success);
    }

    [Fact]
    public void Non_dynamic_result_leaves_dynamic_fields_null()
    {
        var old = new SettingStateResult { IsEnabled = false, Success = true };
        var result = CatalogDetectionStateOverlay.Apply(
            Toggle(), old, new CatalogDetectionResult { StateLabel = "Enabled", Detected = true });
        Assert.Null(result.DynamicOptions);
        Assert.Null(result.DynamicSelection);
    }
}
