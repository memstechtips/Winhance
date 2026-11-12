using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class KeyedSelectionShapeTests
{
    [Fact]
    public void Control_OptionList_DerivesKeyedSelection()
    {
        FakeOptionProvider.SettingFor().Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void Control_PowerPlanSetting_DerivesKeyedSelection()
    {
        // It has no states, so without its option list it would derive an Action button.
        SettingCatalog.Find("power-plan-selection")!.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void InputTypeFor_KeyedSelection_ReturnsSelection()
    {
        ConfigFileMapper.InputTypeFor(FakeOptionProvider.SettingFor()).Should().Be(InputType.Selection);
    }

    [Fact]
    public void Options_FakeProvider_OffersThreeOptionsAndCurrentKeyReadsRegistry()
    {
        var setting = FakeOptionProvider.SettingFor();
        var context = new FakeDetectionContext()
            .Set(FakeOptionProvider.ValuePath, FakeOptionProvider.ValueName, "beta");
        var provider = new FakeOptionProvider();

        provider.Options(setting, context).Select(o => o.Value).Should().Equal("alpha", "beta", "gamma");
        provider.CurrentKey(setting, context).Should().Be("beta");
    }

    [Fact]
    public void SetFor_OfferedKey_WritesKeyOverItsTarget()
    {
        var set = KeyedOptions.SetFor(FakeOptionProvider.SettingFor(), "gamma", new FakeOptionProvider());

        set.Should().NotBeNull();
        set![FakeOptionProvider.TargetKey].WritePayload.Should().Be("gamma");
    }

    [Fact]
    public void SetFor_KeyProviderDoesNotOffer_ReturnsNull()
    {
        KeyedOptions.SetFor(FakeOptionProvider.SettingFor(), "delta", new FakeOptionProvider()).Should().BeNull();
    }

    [Fact]
    public void Keyed_Always_CarriesKeyAndSavedLabel()
    {
        var choice = new ChoiceValue.Keyed("beta", "Beta zone");

        choice.Key.Should().Be("beta");
        choice.Label.Should().Be("Beta zone");
    }
}
