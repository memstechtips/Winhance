using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class BackgroundCatalogTests
{
    private static SettingState WindowsDefaultOf(string settingId) =>
        SettingCatalog.Find(settingId)!.States.Single(s => s.HasRole(RoleKind.WindowsDefault));

    // A profile that has never set a background carries none of these values.
    [Theory]
    [InlineData("theme-wallpaper", "BackgroundType")]
    [InlineData("theme-wallpaper-fit", "TileWallpaper")]
    [InlineData("theme-wallpaper-interval", "Interval")]
    [InlineData("theme-wallpaper-shuffle", "Shuffle")]
    public void The_windows_default_deletes_the_value_it_also_reads_as_absent(string settingId, string targetKey)
    {
        var state = WindowsDefaultOf(settingId);

        state.Set[targetKey].AcceptsAbsent.Should().BeTrue();
        state.ResetSet.Should().NotBeNull();
        state.ResetSet![targetKey].DeleteOnWrite.Should().BeTrue();
    }

    // Every machine carries WallpaperStyle, so a reset writes it rather than deleting it.
    [Fact]
    public void The_fits_style_is_still_written_on_a_reset()
    {
        var state = WindowsDefaultOf("theme-wallpaper-fit");

        state.ResetSet!.Keys.Should().Equal("TileWallpaper");
        state.Set["WallpaperStyle"].WritePayload.Should().Be("10");
    }

    // Measured 2026-09-16: Picture to Spotlight moves BackgroundType 0 to 3 and EnabledState 0 to 1, but writing
    // them does not turn it on. The shell's own background task does that, and an unpackaged app cannot reach it.
    [Fact]
    public void Windows_spotlight_is_detected_but_never_offered()
    {
        var wallpaper = SettingCatalog.Find("theme-wallpaper")!;
        var spotlight = wallpaper.States.Single(s => s.Label == LocKey.Setting.ThemeWallpaper.Option3);

        spotlight.IsDetectOnly.Should().BeTrue("Windows gives apps no supported way to select Spotlight");
        spotlight.Set["BackgroundType"].WritePayload.Should().Be(3);
        spotlight.Set["SpotlightEnabled"].WritePayload.Should().Be(1);
        spotlight.Warning.Should().NotBeNull("the card has to explain why the option is missing");
        spotlight.Links.Should().BeEmpty("a state that cannot be applied can never fire a Requires");
        wallpaper.States.Should().NotContain(s => s.IsFallback);
    }

    // This detect-only state carries a Set, so without the guard an index from an older config would write both
    // values, change nothing on the machine and report success.
    [Fact]
    public void Applying_the_spotlight_index_resolves_to_nothing()
    {
        var wallpaper = SettingCatalog.Find("theme-wallpaper")!;
        int index = wallpaper.States.ToList().FindIndex(s => s.Label == LocKey.Setting.ThemeWallpaper.Option3);

        var ops = ApplyRequestResolver.Resolve("theme-wallpaper", enable: true, value: index, resetToDefault: false);

        ops.Should().NotBeNull("a null would make the caller fail loudly; this is a deliberate no-op");
        ops.Should().BeEmpty();
    }

    [Fact]
    public void Every_other_kind_turns_the_spotlight_switch_off()
    {
        var wallpaper = SettingCatalog.Find("theme-wallpaper")!;

        foreach (var state in wallpaper.States.Where(s => s.Label != LocKey.Setting.ThemeWallpaper.Option3))
            state.Set["SpotlightEnabled"].WritePayload.Should().Be(0, "{0} must not leave Spotlight armed", state.Label.Value);
    }

    [Fact]
    public void The_spotlight_warning_names_the_two_settings_that_hide_it()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")));
        var en = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
        var spotlight = SettingCatalog.Find("theme-wallpaper")!
            .States.Single(s => s.Label == LocKey.Setting.ThemeWallpaper.Option3);

        var text = en[spotlight.Warning!.Value.Value];

        text.Should().Contain(en["Setting_privacy-content-delivery-allowed_Name"]);
        text.Should().Contain(en["Setting_privacy-subscribed-content_Name"]);
    }
}
