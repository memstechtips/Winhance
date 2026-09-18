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
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "A new option list needs a sample key here."),
    };

    [Fact]
    public void Every_option_list_has_the_provider_that_names_it()
    {
        var registry = OptionProviderFixtures.Registry();

        foreach (var source in Enum.GetValues<OptionSource>())
            registry.For(source).Sources.Should().Contain(source);
    }

    [Fact]
    public void An_option_list_no_provider_handles_is_refused_by_name()
    {
        var registry = new OptionProviderRegistry(() => new IOptionProvider[] { new TimeRegionLanguageService() });

        var act = () => registry.For(OptionSource.Pictures);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Pictures*");
    }

    // A provider that cannot compute a target's From value throws at apply time, not when the catalog is written.
    [Fact]
    public void Every_value_a_keyed_setting_takes_is_one_its_provider_computes()
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
    public void Every_keyed_setting_reads_its_options_and_key_off_a_pc_without_throwing()
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
