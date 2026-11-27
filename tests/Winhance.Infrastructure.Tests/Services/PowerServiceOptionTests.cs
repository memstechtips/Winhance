using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerServiceOptionTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static readonly PowerService Service = OptionProviderFixtures.PowerService();

    private static readonly OptionProviderRegistry Registry = new(() => new IOptionProvider[] { Service });

    private static Setting PowerPlan => SettingCatalog.Find("power-plan-selection")!;

    [Fact]
    public void Sources_Always_IsThePowerPlanListOnly()
    {
        Service.Sources.Should().Equal(OptionSource.PowerPlans);
    }

    [Fact]
    public void SetFor_PowerPlanSchemeGuid_WritesNoRegistryValues()
    {
        KeyedOptions.SetFor(PowerPlan, Balanced, Service).Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void EffectFor_PowerPlan_CarriesTheActivation()
    {
        Service.EffectFor(PowerPlan, Balanced)
            .Should().BeOfType<PowerPlanEffect>()
            .Which.Guid.Should().Be(Balanced);
    }

    [Fact]
    public void EffectFor_BracedUpperCaseGuid_BecomesThePlainLowerCaseSchemeGuid()
    {
        Service.EffectFor(PowerPlan, "{381B4222-F694-41F0-9685-FF5BB260DF2E}")
            .Should().BeOfType<PowerPlanEffect>()
            .Which.Guid.Should().Be(Balanced);
    }

    [Theory]
    [InlineData("{381B4222-F694-41F0-9685-FF5BB260DF2E}")]
    [InlineData("381B4222-F694-41F0-9685-FF5BB260DF2E")]
    public void SameOption_SchemeGuidWithAnyBracesOrCase_IsTheSamePlan(string saved)
    {
        Service.SameOption(PowerPlan, new DynamicOption("Balanced", saved), new DynamicOption("Ausbalanciert", Balanced))
            .Should().BeTrue();
    }

    [Fact]
    public void SameOption_WinhancePlanUnderAWindowsAssignedGuid_IsStillTheWinhancePlan()
    {
        var saved = new DynamicOption("Winhance Power Plan", PowerPlanCatalog.WinhancePowerPlanGuid);
        var live = new DynamicOption("Winhance Power Plan", "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11");

        Service.SameOption(PowerPlan, saved, live).Should().BeTrue();
    }

    // fa.json names the scheme around the brand rather than keeping the whole English name, so the brand is all
    // there is to match on; a user's own plan that mentions it is read as the same plan.
    [Fact]
    public void SameOption_PlanNamedAroundTheBrand_IsTheWinhancePlan()
    {
        var saved = new DynamicOption("Winhance Power Plan", PowerPlanCatalog.WinhancePowerPlanGuid);
        var live = new DynamicOption("My Winhance tweaks", "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11");

        Service.SameOption(PowerPlan, saved, live).Should().BeTrue();
    }

    [Fact]
    public void SameOption_TwoDifferentPlans_AreNotTheSameOption()
    {
        var highPerformance = new DynamicOption("High performance", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        var custom = new DynamicOption("Gaming", "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11");

        Service.SameOption(PowerPlan, highPerformance, new DynamicOption("Balanced", Balanced)).Should().BeFalse();
        Service.SameOption(PowerPlan, custom, new DynamicOption("Balanced", Balanced)).Should().BeFalse();
        Service.SameOption(PowerPlan, custom, new DynamicOption("Office", "1d2c3b4a-0000-4000-8000-5e6f7a8b9c0d")).Should().BeFalse();
    }

    [Fact]
    public void SetFor_KeyThatIsNotASchemeGuid_ReturnsNull()
    {
        KeyedOptions.SetFor(PowerPlan, "x", Service).Should().BeNull();
        Service.EffectFor(PowerPlan, "x").Should().BeNull();
    }

    [Fact]
    public void Resolve_PowerPlanSelection_ReturnsASchemeActivationAlone()
    {
        var plan = ApplyRequestResolver.Resolve(
            "power-plan-selection", enable: true, value: Balanced, resetToDefault: false, options: Registry);

        var effect = Assert.IsType<EffectOp>(Assert.Single(plan!)).Effect;
        Assert.IsType<PowerPlanEffect>(effect).Guid.Should().Be(Balanced);
    }

    [Fact]
    public void Resolve_PowerPlanKeyThatIsNotASchemeGuid_ReturnsNull()
    {
        Assert.Null(ApplyRequestResolver.Resolve("power-plan-selection", enable: true, value: "not-a-guid",
            resetToDefault: false, options: Registry));
        Assert.Null(ApplyRequestResolver.Resolve("power-plan-selection", enable: true, value: 1,
            resetToDefault: false, options: Registry));
    }
}
