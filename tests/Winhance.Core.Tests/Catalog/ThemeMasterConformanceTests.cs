using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// theme-mode-windows is a Controls MASTER over two facets it does not exclusively own (AppsUseLightTheme and
// SystemUsesLightTheme are independent): two presets plus a NEUTRAL state for light-apps/dark-shell - the
// Windows 10 shipped default, which has no single write. That state is IsFallback (detection names it) and
// IsDetectOnly (not offered as a pick that would write nothing).
public class ThemeMasterConformanceTests
{
    private const string Master = "theme-mode-windows";
    private const string Apps = "theme-mode-apps";
    private const string System = "theme-mode-system";

    private static Setting S(string id) => SettingCatalog.All.First(s => s.Id == id);

    // Simple in-memory readings: key -> value. A missing key reads as absent.
    private sealed class FakeReadings : IStateReadings
    {
        private readonly Dictionary<string, object?> _present;
        public FakeReadings(Dictionary<string, object?> present) => _present = present;
        public bool TryGet(string key, out object? value, out bool present)
        {
            present = _present.TryGetValue(key, out value);
            return true;
        }
    }

    [Fact]
    public void The_two_presets_keep_their_original_indexes_and_the_neutral_state_is_APPENDED()
    {
        // ConfigurationItem.SelectedIndex persists the RAW state index, the autounattend generator keys off
        // it, and ConfigReviewService hardcodes index 0 == Light. Reordering these silently flips every
        // saved .winhance config on import, which no test would otherwise catch.
        var states = S(Master).States;

        states.Select(st => st.Label).Should().Equal(
            LocKey.Setting.ThemeModeWindows.Option0, LocKey.Setting.ThemeModeWindows.Option1, LocKey.Setting.ThemeModeWindows.Option2);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Each_preset_Controls_both_children_using_the_childrens_OWN_state_labels(
        int presetIndex, bool childrenOn)
    {
        var expectedChildLabel = childrenOn ? LocKey.Common.Enabled : LocKey.Common.Disabled;
        // The children are Enabled/Disabled toggles ("Enabled" == that surface uses the LIGHT theme), NOT
        // Light/Dark. A Controls value naming a label the child does not have is unsatisfiable forever.
        var preset = S(Master).States[presetIndex];

        preset.Controls.Should().NotBeNull();
        preset.Controls!.Should().BeEquivalentTo(new Dictionary<string, LocKey>
        {
            [Apps] = expectedChildLabel,
            [System] = expectedChildLabel,
        });

        foreach (var entry in preset.Controls!)
            S(entry.Key).States.Select(st => st.Label).Should().Contain(entry.Value);
    }

    [Fact]
    public void The_neutral_state_is_detect_only_fallback_and_writes_nothing()
    {
        var neutral = S(Master).States.Single(st => st.IsDetectOnly);

        neutral.Label.Should().Be(LocKey.Setting.ThemeModeWindows.Option2);
        neutral.IsFallback.Should().BeTrue("detection has to land on it instead of reporting Not recognized");
        neutral.Set.Should().BeEmpty("there is no single value that means 'the two facets disagree'");
        neutral.Controls.Should().BeNull("imposing no preset is what makes it the reverse-sync snap target");
        neutral.Roles.Should().BeEmpty("an unchoosable state cannot be recommended or be what Windows ships");
    }

    [Theory]
    [InlineData(1, 0)]  // light apps, dark shell - the Windows 10 shipped default
    [InlineData(0, 1)]  // dark apps, light shell
    public void A_mixed_reading_resolves_to_the_neutral_state(int appsValue, int systemValue)
    {
        var readings = new FakeReadings(new()
        {
            ["AppsUseLightTheme"] = appsValue,
            ["SystemUsesLightTheme"] = systemValue,
        });

        StateDetectionEngine.Detect(S(Master).States, readings).Should().Be(LocKey.Setting.ThemeModeWindows.Option2.Value);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(0, 0, 1)]
    public void A_uniform_reading_still_resolves_to_its_preset(int appsValue, int systemValue, int expectedIndex)
    {
        // Non-vacuity for the test above: the catch-all must not swallow the readings the presets DO explain.
        var readings = new FakeReadings(new()
        {
            ["AppsUseLightTheme"] = appsValue,
            ["SystemUsesLightTheme"] = systemValue,
        });

        StateDetectionEngine.Detect(S(Master).States, readings).Should().Be(S(Master).States[expectedIndex].Label.Value);
    }

    [Fact]
    public void ResolveReverseSync_snaps_the_master_to_the_neutral_state_when_the_children_disagree()
    {
        // A detect-only state has to be selectable as the neutral snap target. ResolveReverseSync picks "the
        // first state imposing no Controls", which is exactly it.
        var actions = RelationshipResolver.ResolveReverseSync(Apps, SettingCatalog.All, id => id switch
        {
            Apps => LocKey.Common.Disabled,
            System => LocKey.Common.Enabled,
            Master => LocKey.Setting.ThemeModeWindows.Option0,
            _ => null,
        });

        actions.Should().ContainSingle(a => a.SettingId == Master && a.StateLabel == LocKey.Setting.ThemeModeWindows.Option2);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void ResolveReverseSync_snaps_the_master_to_the_preset_the_children_now_satisfy(
        bool childrenOff, int expectedPresetIndex)
    {
        var childLabel = childrenOff ? LocKey.Common.Disabled : LocKey.Common.Enabled;
        var actions = RelationshipResolver.ResolveReverseSync(Apps, SettingCatalog.All, id => id switch
        {
            Apps => childLabel,
            System => childLabel,
            Master => LocKey.Setting.ThemeModeWindows.Option2,
            _ => null,
        });

        actions.Should().ContainSingle(a => a.SettingId == Master && a.StateLabel == S(Master).States[expectedPresetIndex].Label);
    }

    [Theory]
    [InlineData(Master)]
    [InlineData(Apps)]
    [InlineData(System)]
    [InlineData("theme-transparency")]
    public void Theme_settings_declare_the_appearance_broadcast_and_no_process_restart(string id)
    {
        // Verified on Windows: the appearance broadcast alone applies the change; the restart only raised the
        // pending-restart bar for a repaint Windows was already doing.
        var setting = S(id);

        setting.Apply.Restart.Should().BeNull();
        setting.Apply.NotifyWindows.HasFlag(WindowsChange.Appearance).Should().BeTrue();
    }
}
