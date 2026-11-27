using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class NavLockPolicyTests
{
    [Theory]
    [InlineData("Autounattend", WinhanceMode.Normal, BuilderTarget.Config, PageGate.EnterBuilder)]
    [InlineData("Autounattend", WinhanceMode.Normal, BuilderTarget.Autounattend, PageGate.EnterBuilder)]
    [InlineData("Autounattend", WinhanceMode.Builder, BuilderTarget.Config, PageGate.SwitchToAutounattend)]
    [InlineData("Autounattend", WinhanceMode.Builder, BuilderTarget.Autounattend, PageGate.Open)]
    [InlineData("Autounattend", WinhanceMode.ConfigReview, BuilderTarget.Config, PageGate.EnterBuilder)]
    [InlineData("Autounattend", WinhanceMode.ConfigReview, BuilderTarget.Autounattend, PageGate.EnterBuilder)]
    [InlineData("WimUtil", WinhanceMode.Normal, BuilderTarget.Config, PageGate.EnterBuilder)]
    [InlineData("WimUtil", WinhanceMode.Normal, BuilderTarget.Autounattend, PageGate.EnterBuilder)]
    [InlineData("WimUtil", WinhanceMode.Builder, BuilderTarget.Config, PageGate.SwitchToAutounattend)]
    [InlineData("WimUtil", WinhanceMode.Builder, BuilderTarget.Autounattend, PageGate.Open)]
    [InlineData("WimUtil", WinhanceMode.ConfigReview, BuilderTarget.Config, PageGate.EnterBuilder)]
    [InlineData("WimUtil", WinhanceMode.ConfigReview, BuilderTarget.Autounattend, PageGate.EnterBuilder)]
    public void GateFor_UnattendAndWimUtil_OpenOnlyWhileBuilderTargetsAutounattend(string tag, WinhanceMode mode, BuilderTarget target, PageGate gate)
    {
        NavLockPolicy.GateFor(tag, mode, target).Should().Be(gate);
    }

    [Theory]
    [InlineData("SoftwareApps")]
    [InlineData("Optimize")]
    [InlineData("Settings")]
    [InlineData(null)]
    public void GateFor_EveryOtherPage_OpensInEveryMode(string? tag)
    {
        foreach (var mode in Enum.GetValues<WinhanceMode>())
            NavLockPolicy.GateFor(tag, mode, BuilderTarget.Config).Should().Be(PageGate.Open);
    }
}
