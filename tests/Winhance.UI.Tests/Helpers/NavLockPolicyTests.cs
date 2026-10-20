using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class NavLockPolicyTests
{
    [Theory]
    [InlineData(WinhanceMode.Normal, BuilderTarget.Config, true)]
    [InlineData(WinhanceMode.Normal, BuilderTarget.Autounattend, true)]
    [InlineData(WinhanceMode.ConfigReview, BuilderTarget.Config, true)]
    [InlineData(WinhanceMode.ConfigReview, BuilderTarget.Autounattend, true)]
    [InlineData(WinhanceMode.Builder, BuilderTarget.Config, true)]
    [InlineData(WinhanceMode.Builder, BuilderTarget.Autounattend, false)]
    public void Autounattend_is_open_only_while_Builder_targets_it(WinhanceMode mode, BuilderTarget target, bool locked)
    {
        NavLockPolicy.IsAutounattendLocked(mode, target).Should().Be(locked);
    }

    [Theory]
    [InlineData(WinhanceMode.Normal, false)]
    [InlineData(WinhanceMode.Builder, false)]
    [InlineData(WinhanceMode.ConfigReview, true)]
    public void WimUtil_is_locked_only_during_Config_Review(WinhanceMode mode, bool locked)
    {
        NavLockPolicy.IsWimUtilLocked(mode).Should().Be(locked);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_locked_button_is_not_invokable_by_automation(bool isLocked, bool invokable)
    {
        NavLockPolicy.IsInvokableByAutomation(isLocked).Should().Be(invokable);
    }

    [Fact]
    public void A_locked_button_carries_its_tooltip_as_automation_help_text()
    {
        NavLockPolicy.HelpTextFor(true, "Available in Builder mode with Autounattend selected")
            .Should().Be("Available in Builder mode with Autounattend selected");
    }

    [Fact]
    public void An_unlocked_button_carries_no_help_text_even_when_a_tooltip_is_passed()
    {
        NavLockPolicy.HelpTextFor(false, "Available in Builder mode with Autounattend selected")
            .Should().BeEmpty();
    }

    [Fact]
    public void A_locked_button_with_no_tooltip_carries_no_help_text_rather_than_null()
    {
        // The value is handed straight to AutomationProperties.SetHelpText, which takes a string.
        NavLockPolicy.HelpTextFor(true, null).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void Only_the_locked_to_unlocked_edge_is_announced(bool wasLocked, bool isLocked, bool announce)
    {
        // ApplyNavLocks re-applies the whole matrix on every ModeChanged, so an unconditional announcement repeats.
        NavLockPolicy.ShouldAnnounceUnlock(wasLocked, isLocked).Should().Be(announce);
    }

    [Fact]
    public void The_announcement_puts_the_button_name_into_the_localized_format()
    {
        NavLockPolicy.UnlockAnnouncement("{0} is now available", "Autounattend")
            .Should().Be("Autounattend is now available");
    }

    [Fact]
    public void A_translated_format_that_reorders_or_repeats_the_name_still_works()
    {
        NavLockPolicy.UnlockAnnouncement("{0}: {0} verfuegbar", "Autounattend")
            .Should().Be("Autounattend: Autounattend verfuegbar");
    }

    [Fact]
    public void A_broken_translated_format_falls_back_to_the_button_name_rather_than_throwing()
    {
        NavLockPolicy.UnlockAnnouncement("{0 is now available", "Autounattend").Should().Be("Autounattend");
    }
}
