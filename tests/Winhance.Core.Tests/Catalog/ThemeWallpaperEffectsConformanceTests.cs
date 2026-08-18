using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// The OS-divergent default wallpaper for theme-mode-windows lives on the catalog as a per-state, build-gated
// WallpaperEffect; ThemeWallpaperApplier resolves the active state's build-matching effect.
public class ThemeWallpaperEffectsConformanceTests
{
    private static SettingState State(string label) =>
        SettingCatalog.All.First(s => s.Id == SettingIds.ThemeModeWindows).States.First(st => st.Label == label);

    private static string PathForOs(string label, BuildRange os) =>
        State(label).Effects.OfType<WallpaperEffect>().Single(e => e.AppliesTo.Single() == os).Path;

    [Theory]
    [InlineData("Light Mode", @"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg")]
    [InlineData("Dark Mode", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg", @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg")]
    public void Each_theme_state_carries_the_per_OS_default_wallpaper(string label, string win11Path, string win10Path)
    {
        PathForOs(label, BuildRange.Windows11).Should().Be(win11Path);
        PathForOs(label, BuildRange.Windows10).Should().Be(win10Path);
    }

    [Fact]
    public void Only_the_two_theme_states_carry_wallpaper_effects()
    {
        var carriers = SettingCatalog.All
            .SelectMany(s => s.States.Select(st => (s.Id, st.Label, Has: st.Effects.OfType<WallpaperEffect>().Any())))
            .Where(x => x.Has)
            .Select(x => (x.Id, x.Label))
            .ToList();

        carriers.Should().BeEquivalentTo(new[]
        {
            (SettingIds.ThemeModeWindows, "Light Mode"),
            (SettingIds.ThemeModeWindows, "Dark Mode"),
        });
    }

    // ThemeWallpaperApplier runs this setting through the synchronous ApplyExecutor and cannot await, so a
    // process-launching effect added here would be split off the plan and never run.
    [Fact]
    public void No_theme_state_carries_an_effect_the_synchronous_apply_path_cannot_run()
    {
        SettingCatalog.All.First(s => s.Id == SettingIds.ThemeModeWindows)
            .States.SelectMany(st => st.Effects)
            .Where(e => e.IsAsyncIo)
            .Should().BeEmpty();
    }
}
