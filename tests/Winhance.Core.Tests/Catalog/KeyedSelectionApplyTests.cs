using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class KeyedSelectionApplyTests
{
    private static readonly Setting[] Catalog = [FakeOptionProvider.SettingFor()];

    private static readonly FakeOptionProviderRegistry Registry = new();

    private static FakeOptionProviderRegistry Nudging(Effect effect)
    {
        var provider = new Mock<IOptionProvider>();
        provider.Setup(p => p.Accepts(It.IsAny<Setting>(), It.IsAny<string>())).Returns(true);
        provider.Setup(p => p.EffectFor(It.IsAny<Setting>(), It.IsAny<string>())).Returns(effect);
        return new FakeOptionProviderRegistry(provider.Object);
    }

    [Fact]
    public void A_chosen_key_becomes_the_registry_write_its_target_declares()
    {
        var plan = ApplyRequestResolver.Resolve(
            "fake-keyed", enable: true, value: "beta", resetToDefault: false, Catalog, options: Registry);

        var write = Assert.IsType<RegistryWriteOp>(Assert.Single(plan!));
        write.Path.Should().Be(FakeOptionProvider.ValuePath);
        write.Target.ValueName.Should().Be(FakeOptionProvider.ValueName);
        write.Value.Should().Be("beta");
    }

    [Fact]
    public void A_key_the_provider_will_not_accept_resolves_to_no_plan()
    {
        var plan = ApplyRequestResolver.Resolve(
            "fake-keyed", enable: true, value: "delta", resetToDefault: false, Catalog, options: Registry);

        plan.Should().BeNull();
    }

    [Fact]
    public void An_index_is_not_a_key_and_resolves_to_no_plan()
    {
        var plan = ApplyRequestResolver.Resolve(
            "fake-keyed", enable: true, value: 0, resetToDefault: false, Catalog, options: Registry);

        plan.Should().BeNull();
    }

    [Fact]
    public void A_key_resolves_to_no_plan_when_there_is_no_provider_to_turn_it_into_values()
    {
        var plan = ApplyRequestResolver.Resolve(
            "fake-keyed", enable: true, value: "beta", resetToDefault: false, Catalog);

        plan.Should().BeNull();
    }

    [Fact]
    public void The_power_plan_resolves_to_the_providers_activation_alone()
    {
        const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";

        var plan = ApplyRequestResolver.Resolve(
            "power-plan-selection", enable: true, value: guid, resetToDefault: false,
            options: Nudging(new PowerPlanEffect(guid)));

        var effect = Assert.IsType<EffectOp>(Assert.Single(plan!)).Effect;
        Assert.IsType<PowerPlanEffect>(effect).Guid.Should().Be(guid);
    }

    [Fact]
    public void Applying_the_time_zone_setting_emits_the_registry_write_before_the_nudge()
    {
        var nudge = new ScriptEffect("tzutil.exe /s \"UTC\"", RunContext.System);

        var plan = ApplyRequestResolver.Resolve(
            "region-time-zone", enable: true, value: "UTC", resetToDefault: false, options: Nudging(nudge));

        plan.Should().HaveCount(2);
        var write = Assert.IsType<RegistryWriteOp>(plan![0]);
        write.Target.ValueName.Should().Be("TimeZoneKeyName");
        write.Value.Should().Be("UTC");
        Assert.IsType<EffectOp>(plan[1]).Effect.Should().BeSameAs(nudge);
    }
}
