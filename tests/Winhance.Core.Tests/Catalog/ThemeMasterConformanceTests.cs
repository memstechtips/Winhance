using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>
/// theme-mode-windows is a Controls MASTER over two facets it does not exclusively own, not a parent that
/// gates them - the same shape visual-effects-mode already uses. It carries two presets (Light / Dark) plus
/// a NEUTRAL state for the reading neither preset explains: AppsUseLightTheme and SystemUsesLightTheme are
/// independent, so light-apps/dark-shell is a real configuration (it is the Windows 10 shipped default) that
/// this setting has no single write for. That state is IsFallback (detection lands on it and NAMES it, where
/// the card used to read "Not recognized") and IsDetectOnly (it is not offered as a pick that would write
/// nothing).
///
/// Machine-independent: everything here reads the shipped <see cref="SettingCatalog"/> and the pure
/// resolvers.
/// </summary>
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

    // ---- state ORDER is a public contract -------------------------------------------------------

    [Fact]
    public void The_two_presets_keep_their_original_indexes_and_the_neutral_state_is_APPENDED()
    {
        // ConfigurationItem.SelectedIndex persists the RAW state index, the autounattend generator keys off
        // it, and ConfigReviewService hardcodes index 0 == Light. Reordering these silently flips every
        // saved .winhance config on import, which no test would otherwise catch.
        var states = S(Master).States;

        states.Select(st => st.Label).Should().Equal("Light Mode", "Dark Mode", "Mixed");
    }

    // ---- the presets declare what the children must be ------------------------------------------

    [Theory]
    [InlineData("Light Mode", "Enabled")]
    [InlineData("Dark Mode", "Disabled")]
    public void Each_preset_Controls_both_children_using_the_childrens_OWN_state_labels(
        string presetLabel, string expectedChildLabel)
    {
        // The children are Enabled/Disabled toggles ("Enabled" == that surface uses the LIGHT theme), NOT
        // Light/Dark. A Controls value naming a label the child does not have is unsatisfiable forever.
        var preset = S(Master).States.First(st => st.Label == presetLabel);

        preset.Controls.Should().NotBeNull();
        preset.Controls!.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [Apps] = expectedChildLabel,
            [System] = expectedChildLabel,
        });

        foreach (var entry in preset.Controls!)
            S(entry.Key).States.Select(st => st.Label).Should().Contain(entry.Value);
    }

    // ---- the neutral state ----------------------------------------------------------------------

    [Fact]
    public void The_neutral_state_is_detect_only_fallback_and_writes_nothing()
    {
        var neutral = S(Master).States.Single(st => st.IsDetectOnly);

        neutral.Label.Should().Be("Mixed");
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

        StateDetectionEngine.Detect(S(Master).States, readings).Should().Be("Mixed");
    }

    [Theory]
    [InlineData(1, 1, "Light Mode")]
    [InlineData(0, 0, "Dark Mode")]
    public void A_uniform_reading_still_resolves_to_its_preset(int appsValue, int systemValue, string expected)
    {
        // Non-vacuity for the test above: the catch-all must not swallow the readings the presets DO explain.
        var readings = new FakeReadings(new()
        {
            ["AppsUseLightTheme"] = appsValue,
            ["SystemUsesLightTheme"] = systemValue,
        });

        StateDetectionEngine.Detect(S(Master).States, readings).Should().Be(expected);
    }

    // ---- reverse sync ---------------------------------------------------------------------------

    [Fact]
    public void ResolveReverseSync_snaps_the_master_to_the_neutral_state_when_the_children_disagree()
    {
        // The STOP condition the plan named: a detect-only state has to be selectable as the neutral snap
        // target. ResolveReverseSync picks "the first state imposing no Controls", which is exactly it.
        var actions = RelationshipResolver.ResolveReverseSync(Apps, SettingCatalog.All, id => id switch
        {
            Apps => "Disabled",
            System => "Enabled",
            Master => "Light Mode",
            _ => null,
        });

        actions.Should().ContainSingle(a => a.SettingId == Master && a.StateLabel == "Mixed");
    }

    [Theory]
    [InlineData("Enabled", "Light Mode")]
    [InlineData("Disabled", "Dark Mode")]
    public void ResolveReverseSync_snaps_the_master_to_the_preset_the_children_now_satisfy(
        string childLabel, string expectedPreset)
    {
        var actions = RelationshipResolver.ResolveReverseSync(Apps, SettingCatalog.All, id => id switch
        {
            Apps => childLabel,
            System => childLabel,
            Master => "Mixed",
            _ => null,
        });

        actions.Should().ContainSingle(a => a.SettingId == Master && a.StateLabel == expectedPreset);
    }

    // ---- A3: the Explorer restart is gone, the appearance broadcast is not ----------------------

    [Theory]
    [InlineData(Master)]
    [InlineData(Apps)]
    [InlineData(System)]
    [InlineData("theme-transparency")]
    public void Theme_settings_declare_the_appearance_broadcast_and_no_process_restart(string id)
    {
        // Marco verified on Windows (2026-07-31) that the appearance broadcast alone applies the change.
        // The restart only raised the pending-restart bar for a repaint Windows was already doing.
        var setting = S(id);

        setting.Apply.Restart.Should().BeNull();
        setting.Apply.NotifyWindows.HasFlag(WindowsChange.Appearance).Should().BeTrue();
    }
}
