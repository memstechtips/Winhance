using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class KeyedOptionsTests
{
    private const string Win11Light = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";
    private const string Win11Dark = @"C:\Windows\Web\Wallpaper\Windows\img19.jpg";
    private const string Win10 = @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg";

    private static Setting Picture => SettingCatalog.Find("theme-wallpaper-picture")!;

    private static Setting Color => SettingCatalog.Find("theme-wallpaper-color")!;

    private static IOptionProvider Computing(Func<OptionValue, string, string?> valueFor)
    {
        var provider = new Mock<IOptionProvider>();
        provider.Setup(p => p.Accepts(It.IsAny<Setting>(), It.IsAny<string>())).Returns(true);
        provider.Setup(p => p.ValueFor(It.IsAny<OptionValue>(), It.IsAny<string>())).Returns(valueFor);
        return provider.Object;
    }

    private static Dictionary<string, object?> Payloads(IReadOnlyDictionary<string, StateValue>? set) =>
        set.Should().NotBeNull().And.Subject.As<IReadOnlyDictionary<string, StateValue>>()
            .ToDictionary(kv => kv.Key, kv => kv.Value.WritePayload);

    [Fact]
    public void Each_windows_picture_is_named_by_its_path()
    {
        Picture.States.Select(s => KeyedOptions.KeyOf(Picture, s)).Should().Equal(Win11Light, Win11Dark, Win10);
    }

    [Fact]
    public void The_key_is_read_from_the_target_detection_reads_not_the_copy_only_an_apply_writes()
    {
        var reordered = Picture with { Targets = [.. Picture.Targets.OrderBy(t => t.Key != "CurrentWallpaperPath")] };

        reordered.Targets[0].Key.Should().Be("CurrentWallpaperPath");
        KeyedOptions.KeyOf(reordered, reordered.States[0]).Should().Be(Win11Light);
    }

    [Fact]
    public void A_colour_state_names_no_key_because_its_target_takes_a_computed_value()
    {
        var black = new SettingState
        {
            Label = TestKeys.Of("Black"),
            Set = new Dictionary<string, StateValue> { ["Background"] = StateValue.Of("#000000") },
        };

        KeyedOptions.KeyOf(Color, black).Should().BeNull();
    }

    [Fact]
    public void A_picture_writes_the_wallpaper_and_the_path_windows_restores_it_from()
    {
        var set = KeyedOptions.SetFor(Picture, Win10, Computing((_, _) => throw new InvalidOperationException()));

        Payloads(set).Should().Equal(new Dictionary<string, object?> { ["WallPaper"] = Win10, ["CurrentWallpaperPath"] = Win10 });
    }

    [Fact]
    public void A_colour_writes_what_its_provider_computes_from_the_hex()
    {
        var set = KeyedOptions.SetFor(Color, "#010203",
            Computing((value, key) => value == OptionValue.Color && key == "#010203" ? "1 2 3" : null));

        Payloads(set).Should().Equal(new Dictionary<string, object?> { ["Background"] = "1 2 3" });
    }

    [Fact]
    public void The_regional_format_writes_every_computed_value_and_leaves_one_computed_as_null_standing()
    {
        var format = SettingCatalog.Find("region-format")!;

        var set = KeyedOptions.SetFor(format, "en-US",
            Computing((value, _) => value == OptionValue.LocaleId ? null : value.ToString()));

        var payloads = Payloads(set);
        payloads.Should().HaveCount(30).And.NotContainKey("Locale");
        payloads["LocaleName"].Should().Be("en-US");
        payloads["sShortDate"].Should().Be(nameof(OptionValue.ShortDate));
        payloads["sNegativeSign"].Should().Be(nameof(OptionValue.NegativeSign));
    }

    [Fact]
    public void A_key_the_provider_does_not_accept_writes_nothing()
    {
        var zone = SettingCatalog.Find("region-time-zone")!;

        KeyedOptions.SetFor(zone, "UTC", new FakeOptionProvider()).Should().BeNull();
        Payloads(KeyedOptions.SetFor(zone, "beta", new FakeOptionProvider()))
            .Should().Equal(new Dictionary<string, object?> { ["TimeZoneKeyName"] = "beta" });
    }

    [Fact]
    public void A_power_plan_writes_nothing_for_a_key_it_accepts()
    {
        var set = KeyedOptions.SetFor(SettingCatalog.Find("power-plan-selection")!, "381b4222-f694-41f0-9685-ff5bb260df2e",
            Computing((_, _) => throw new InvalidOperationException()));

        set.Should().NotBeNull().And.BeEmpty();
    }
}
