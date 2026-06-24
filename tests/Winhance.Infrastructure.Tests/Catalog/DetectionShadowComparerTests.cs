using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

public class DetectionShadowComparerTests
{
    private static Setting NewSetting(string id) => new()
    {
        Id = id,
        Display = new() { Name = id, Description = id },
    };

    [Fact]
    public void Toggle_matching_enabled_state_is_a_match()
    {
        var def = new SettingDefinition { Id = "t", Name = "t", Description = "t", InputType = InputType.Toggle };
        var old = new SettingStateResult { Success = true, IsEnabled = true };
        var newResult = new CatalogDetectionResult { StateLabel = "Enabled", Detected = true };

        var row = DetectionShadowComparer.Compare(def, old, NewSetting("t"), newResult);

        Assert.Equal(ShadowVerdict.Match, row.Verdict);
    }

    [Fact]
    public void Toggle_divergence_is_a_diff_with_both_states()
    {
        var def = new SettingDefinition { Id = "t", Name = "t", Description = "t", InputType = InputType.Toggle };
        var old = new SettingStateResult { Success = true, IsEnabled = false };          // old: Disabled
        var newResult = new CatalogDetectionResult { StateLabel = null, Detected = false }; // new: Custom

        var row = DetectionShadowComparer.Compare(def, old, NewSetting("t"), newResult);

        Assert.Equal(ShadowVerdict.Diff, row.Verdict);
        Assert.Equal("Disabled", row.OldState);
        Assert.Equal("Custom", row.NewState);
    }

    [Fact]
    public void Selection_compares_the_resolved_option_label()
    {
        var def = new SettingDefinition
        {
            Id = "s",
            Name = "s",
            Description = "s",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "Off" },
                    new ComboBoxOption { DisplayName = "On" },
                },
            },
        };
        var old = new SettingStateResult { Success = true, CurrentValue = 1 };   // index 1 -> "On"
        var newResult = new CatalogDetectionResult { StateLabel = "On", Detected = true };

        var row = DetectionShadowComparer.Compare(def, old, NewSetting("s"), newResult);

        Assert.Equal(ShadowVerdict.Match, row.Verdict);
        Assert.Equal("On", row.OldState);
    }

    [Fact]
    public void Selection_out_of_range_index_reads_as_custom()
    {
        var def = new SettingDefinition
        {
            Id = "s",
            Name = "s",
            Description = "s",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata { Options = new[] { new ComboBoxOption { DisplayName = "Off" } } },
        };
        var old = new SettingStateResult { Success = true, CurrentValue = -1 };  // no match -> custom
        var newResult = new CatalogDetectionResult { StateLabel = null, Detected = false };

        var row = DetectionShadowComparer.Compare(def, old, NewSetting("s"), newResult);

        Assert.Equal(ShadowVerdict.Match, row.Verdict);   // both "Custom"
        Assert.Equal("Custom", row.OldState);
    }

    [Fact]
    public void Numeric_compares_raw_values()
    {
        var def = new SettingDefinition { Id = "n", Name = "n", Description = "n", InputType = InputType.NumericRange };
        var old = new SettingStateResult { Success = true, CurrentValue = 30 };

        var match = DetectionShadowComparer.Compare(def, old, NewSetting("n"),
            new CatalogDetectionResult { Value = 30, Detected = true });
        var diff = DetectionShadowComparer.Compare(def, old, NewSetting("n"),
            new CatalogDetectionResult { Value = 45, Detected = true });

        Assert.Equal(ShadowVerdict.Match, match.Verdict);
        Assert.Equal(ShadowVerdict.Diff, diff.Verdict);
    }

    [Fact]
    public void Unpaired_when_there_is_no_new_setting()
    {
        var def = new SettingDefinition { Id = "x-win10", Name = "x", Description = "x", InputType = InputType.Toggle };
        var old = new SettingStateResult { Success = true, IsEnabled = true };

        var row = DetectionShadowComparer.Compare(def, old, null, null);

        Assert.Equal(ShadowVerdict.Unpaired, row.Verdict);
    }

    [Fact]
    public void Action_is_skipped()
    {
        var def = new SettingDefinition { Id = "a", Name = "a", Description = "a", InputType = InputType.Action };
        var old = new SettingStateResult { Success = true };

        var row = DetectionShadowComparer.Compare(def, old, NewSetting("a"),
            new CatalogDetectionResult { Detected = false });

        Assert.Equal(ShadowVerdict.Skipped, row.Verdict);
    }
}
