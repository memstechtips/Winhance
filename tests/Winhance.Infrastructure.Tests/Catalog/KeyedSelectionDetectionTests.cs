using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

public class KeyedSelectionDetectionTests
{
    private const string ZonesKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\Zones";

    private static readonly Setting[] OneKeyedSetting = [FakeOptionProvider.SettingFor()];

    private static CatalogDetectionService ServiceOver(FakeDetectionContext context) =>
        new(context.AsFactory(), new Mock<ILogService>().Object, new FakeOptionProviderRegistry(), Mock.Of<IWindowsThemeService>());

    [Fact]
    public async Task Detection_reports_the_key_the_provider_reads_and_the_options_it_offers()
    {
        var context = new FakeDetectionContext()
            .Set(FakeOptionProvider.ValuePath, FakeOptionProvider.ValueName, "gamma");

        var results = await ServiceOver(context).DetectAsync(OneKeyedSetting);

        var result = results["fake-keyed"];
        result.Detected.Should().BeTrue();
        result.StateLabel.Should().Be("gamma");
        result.Options.Should().NotBeNull();
        result.Options!.Select(o => o.Value).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task A_machine_with_no_value_written_reports_no_selection_but_still_offers_the_options()
    {
        var results = await ServiceOver(new FakeDetectionContext()).DetectAsync(OneKeyedSetting);

        var result = results["fake-keyed"];
        result.Detected.Should().BeFalse();
        result.StateLabel.Should().BeNull();
        result.Options!.Should().HaveCount(3);
    }

    [Fact]
    public void The_sub_key_names_a_read_returns_do_not_alias_the_stored_array()
    {
        var context = new FakeDetectionContext().Sub(ZonesKey, "beta", "alpha");

        var names = context.GetSubKeyNames(ZonesKey);
        Array.Sort(names, StringComparer.Ordinal);

        context.GetSubKeyNames(ZonesKey).Should().Equal("beta", "alpha");
    }

    // Matches WindowsRegistryService.ParseKeyPath, which treats HKCU and HKEY_CURRENT_USER as one key.
    [Fact]
    public async Task A_value_set_under_the_long_hive_form_is_read_under_the_short_form()
    {
        var context = new FakeDetectionContext()
            .Set(@"HKEY_CURRENT_USER\Software\Winhance\Keyed", FakeOptionProvider.ValueName, "beta");

        context.GetValue(@"HKCU\Software\Winhance\Keyed", FakeOptionProvider.ValueName).Should().Be("beta");
    }
}
