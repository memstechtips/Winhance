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
    public void The_power_service_provides_the_power_plan_list_and_no_other()
    {
        Service.Sources.Should().Equal(OptionSource.PowerPlans);
    }

    [Fact]
    public void The_power_plan_writes_no_registry_values_for_a_scheme_guid()
    {
        KeyedOptions.SetFor(PowerPlan, Balanced, Service).Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void The_power_plan_carries_the_activation_in_the_effect()
    {
        Service.EffectFor(PowerPlan, Balanced)
            .Should().BeOfType<PowerPlanEffect>()
            .Which.Guid.Should().Be(Balanced);
    }

    [Fact]
    public void A_braced_upper_case_guid_becomes_the_plain_lower_case_scheme_guid()
    {
        Service.EffectFor(PowerPlan, "{381B4222-F694-41F0-9685-FF5BB260DF2E}")
            .Should().BeOfType<PowerPlanEffect>()
            .Which.Guid.Should().Be(Balanced);
    }

    [Theory]
    [InlineData("{381B4222-F694-41F0-9685-FF5BB260DF2E}")]
    [InlineData("381B4222-F694-41F0-9685-FF5BB260DF2E")]
    public void A_scheme_guid_is_the_same_plan_whatever_its_braces_or_case(string saved)
    {
        Service.SameOption(PowerPlan, new DynamicOption("Balanced", saved), new DynamicOption("Ausbalanciert", Balanced))
            .Should().BeTrue();
    }

    [Fact]
    public void The_Winhance_plan_under_a_guid_Windows_assigned_is_still_the_Winhance_plan()
    {
        var saved = new DynamicOption("Winhance Power Plan", PowerPlanCatalog.WinhancePowerPlanGuid);
        var live = new DynamicOption("Winhance Power Plan", "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11");

        Service.SameOption(PowerPlan, saved, live).Should().BeTrue();
    }

    // fa.json names the scheme around the brand rather than keeping the whole English name, so the brand is all
    // there is to match on; a user's own plan that mentions it is read as the same plan.
    [Fact]
    public void A_plan_named_around_the_brand_is_the_Winhance_plan()
    {
        var saved = new DynamicOption("Winhance Power Plan", PowerPlanCatalog.WinhancePowerPlanGuid);
        var live = new DynamicOption("My Winhance tweaks", "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11");

        Service.SameOption(PowerPlan, saved, live).Should().BeTrue();
    }

    [Fact]
    public void Two_different_plans_are_not_the_same_option()
    {
        var highPerformance = new DynamicOption("High performance", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        var custom = new DynamicOption("Gaming", "9f3c1a52-7b1e-4c55-8f0a-2d6e4b7a9c11");

        Service.SameOption(PowerPlan, highPerformance, new DynamicOption("Balanced", Balanced)).Should().BeFalse();
        Service.SameOption(PowerPlan, custom, new DynamicOption("Balanced", Balanced)).Should().BeFalse();
        Service.SameOption(PowerPlan, custom, new DynamicOption("Office", "1d2c3b4a-0000-4000-8000-5e6f7a8b9c0d")).Should().BeFalse();
    }

    [Fact]
    public void The_power_plan_refuses_a_key_that_is_not_a_scheme_guid()
    {
        KeyedOptions.SetFor(PowerPlan, "x", Service).Should().BeNull();
        Service.EffectFor(PowerPlan, "x").Should().BeNull();
    }

    [Fact]
    public void The_power_plan_resolves_to_a_scheme_activation_alone()
    {
        var plan = ApplyRequestResolver.Resolve(
            "power-plan-selection", enable: true, value: Balanced, resetToDefault: false, options: Registry);

        var effect = Assert.IsType<EffectOp>(Assert.Single(plan!)).Effect;
        Assert.IsType<PowerPlanEffect>(effect).Guid.Should().Be(Balanced);
    }

    [Fact]
    public void A_power_plan_key_that_is_not_a_scheme_guid_resolves_to_no_plan()
    {
        Assert.Null(ApplyRequestResolver.Resolve("power-plan-selection", enable: true, value: "not-a-guid",
            resetToDefault: false, options: Registry));
        Assert.Null(ApplyRequestResolver.Resolve("power-plan-selection", enable: true, value: 1,
            resetToDefault: false, options: Registry));
    }
}
