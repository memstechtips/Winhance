using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class OptionProviderRegistryTests
{
    private static string SampleKey(OptionSource source) => source switch
    {
        OptionSource.PowerPlans => PowerPlanCatalog.WinhancePowerPlanGuid,
        OptionSource.TimeZones => "UTC",
        OptionSource.KeyboardLayouts => "00000409",
        OptionSource.RegionalFormats => "en-US",
        OptionSource.SystemLocales => "en-US",
        OptionSource.Countries => "US",
        OptionSource.Pictures => @"C:\Windows\Web\Wallpaper\Windows\img0.jpg",
        OptionSource.Colors => "#FF8C00",
        OptionSource.LocaleValues => "dd/MM/yyyy",
        OptionSource.DisplayLanguages => "en-US",
        OptionSource.VolumeLabels => "Data",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "A new option list needs a sample key here."),
    };

    [Fact]
    public void For_EveryOptionSource_ReturnsTheProviderThatNamesIt()
    {
        var registry = OptionProviderFixtures.Registry();

        foreach (var source in Enum.GetValues<OptionSource>())
            registry.For(source).Sources.Should().Contain(source);
    }

    [Fact]
    public void For_SourceNoProviderHandles_ThrowsNamingTheSource()
    {
        var registry = new OptionProviderRegistry(() => new IOptionProvider[] { new TimeRegionLanguageService() });

        var act = () => registry.For(OptionSource.Pictures);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Pictures*");
    }

    // A provider that cannot compute a target's From value throws at apply time, not when the catalog is written.
    [Fact]
    public void ValueFor_EveryValueAKeyedSettingTakes_IsOneItsProviderComputes()
    {
        var registry = OptionProviderFixtures.Registry();
        var keyed = SettingCatalog.All.Where(s => s.Control == ControlKind.KeyedSelection).ToList();
        keyed.Should().NotBeEmpty();

        foreach (var setting in keyed)
        {
            var source = setting.Options!.Source;
            var provider = registry.For(source);
            var key = SampleKey(source);

            provider.Accepts(setting, key).Should().BeTrue($"{setting.Id} accepts {key}");
            foreach (var target in setting.Targets.OfType<RegTarget>().Where(t => !t.ReadOnly && t.From != OptionValue.Key))
            {
                provider.Invoking(p => p.ValueFor(target.From, key))
                    .Should().NotThrow($"{setting.Id} computes {target.Key} from {target.From}");
            }

            KeyedOptions.SetFor(setting, key, provider).Should().NotBeNull(setting.Id);
        }
    }

    [Fact]
    public void Options_EveryKeyedSetting_ReadsOptionsAndKeyOffAPcWithoutThrowing()
    {
        var registry = OptionProviderFixtures.Registry();

        foreach (var setting in SettingCatalog.All.Where(s => s.Control == ControlKind.KeyedSelection))
        {
            var provider = registry.For(setting.Options!.Source);
            var machine = new FakeDetectionContext();

            provider.Invoking(p => p.Options(setting, machine)).Should().NotThrow(setting.Id);
            provider.Invoking(p => p.CurrentKey(setting, machine)).Should().NotThrow(setting.Id);
        }
    }
}
