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
    public void KeyOf_WindowsPictureStates_NamesEachByItsPath()
    {
        Picture.States.Select(s => KeyedOptions.KeyOf(Picture, s)).Should().Equal(Win11Light, Win11Dark, Win10);
    }

    [Fact]
    public void KeyOf_ReorderedTargets_ReadsTheTargetDetectionReads()
    {
        var reordered = Picture with { Targets = [.. Picture.Targets.OrderBy(t => t.Key != "CurrentWallpaperPath")] };

        reordered.Targets[0].Key.Should().Be("CurrentWallpaperPath");
        KeyedOptions.KeyOf(reordered, reordered.States[0]).Should().Be(Win11Light);
    }

    [Fact]
    public void KeyOf_ColourState_ReturnsNull()
    {
        var black = new SettingState
        {
            Label = TestKeys.Of("Black"),
            Set = new Dictionary<string, StateValue> { ["Background"] = StateValue.Of("#000000") },
        };

        KeyedOptions.KeyOf(Color, black).Should().BeNull();
    }

    [Fact]
    public void SetFor_Picture_WritesTheWallpaperAndTheRestorePath()
    {
        var set = KeyedOptions.SetFor(Picture, Win10, Computing((_, _) => throw new InvalidOperationException()));

        Payloads(set).Should().Equal(new Dictionary<string, object?> { ["WallPaper"] = Win10, ["CurrentWallpaperPath"] = Win10 });
    }

    [Fact]
    public void SetFor_Colour_WritesWhatTheProviderComputesFromTheHex()
    {
        var set = KeyedOptions.SetFor(Color, "#010203",
            Computing((value, key) => value == OptionValue.Color && key == "#010203" ? "1 2 3" : null));

        Payloads(set).Should().Equal(new Dictionary<string, object?> { ["Background"] = "1 2 3" });
    }

    [Fact]
    public void SetFor_RegionalFormat_WritesEveryComputedValueAndSkipsOneComputedAsNull()
    {
        var format = SettingCatalog.Find("region-format")!;

        var set = KeyedOptions.SetFor(format, "en-US",
            Computing((value, _) => value == OptionValue.LocaleId ? null : value.ToString()));

        var payloads = Payloads(set);
        payloads.Should().HaveCount(39).And.NotContainKey("Locale");
        payloads["LocaleName"].Should().Be("en-US");
        payloads["sShortDate"].Should().Be(nameof(OptionValue.ShortDate));
        payloads["sNegativeSign"].Should().Be(nameof(OptionValue.NegativeSign));
        payloads["sNativeDigits"].Should().Be(nameof(OptionValue.NativeDigits));
    }

    [Fact]
    public void SetFor_KeyTheProviderRejects_ReturnsNull()
    {
        var zone = SettingCatalog.Find("region-time-zone")!;

        KeyedOptions.SetFor(zone, "UTC", new FakeOptionProvider()).Should().BeNull();
        Payloads(KeyedOptions.SetFor(zone, "beta", new FakeOptionProvider()))
            .Should().Equal(new Dictionary<string, object?> { ["TimeZoneKeyName"] = "beta" });
    }

    [Fact]
    public void SetFor_PowerPlanWithAcceptedKey_ReturnsAnEmptySet()
    {
        var set = KeyedOptions.SetFor(SettingCatalog.Find("power-plan-selection")!, "381b4222-f694-41f0-9685-ff5bb260df2e",
            Computing((_, _) => throw new InvalidOperationException()));

        set.Should().NotBeNull().And.BeEmpty();
    }
}
