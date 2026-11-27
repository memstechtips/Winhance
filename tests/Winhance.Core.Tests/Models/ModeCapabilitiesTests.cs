using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class ModeCapabilitiesTests
{
    // Each mode's row, stated once. If a capability's meaning is ever changed, exactly one of
    // these should move — a change that flips two rows at once is a sign the capability is
    // conflating two different questions.

    [Fact]
    public void Normal_IsTheOnlyModeThatReachesTheLiveSystem()
    {
        var normal = ModeCapabilities.For(WinhanceMode.Normal);

        normal.AppliesToSystem.Should().BeTrue();
        normal.AuthorsIntent.Should().BeFalse();
        normal.SettingsEditable.Should().BeTrue();
    }

    [Fact]
    public void Builder_AuthorsWithoutApplying()
    {
        var builder = ModeCapabilities.For(WinhanceMode.Builder);

        // The whole contract of Builder mode: nothing reaches this PC...
        builder.AppliesToSystem.Should().BeFalse();
        // ...but the user is still editing, and what they set is intent to be recorded.
        builder.AuthorsIntent.Should().BeTrue();
        builder.SettingsEditable.Should().BeTrue();
    }

    [Fact]
    public void ConfigReview_IsReadOnlyAndAuthorsNothing()
    {
        var review = ModeCapabilities.For(WinhanceMode.ConfigReview);

        review.AppliesToSystem.Should().BeFalse();
        review.AuthorsIntent.Should().BeFalse();
        // The pending decision is accept/reject, not edit — hence the disabled cards.
        review.SettingsEditable.Should().BeFalse();
    }

    [Fact]
    public void AppliesToSystemAndAuthorsIntent_AreNeverBothTrue()
    {
        // Applying and recording-instead-of-applying are mutually exclusive by construction.
        // A mode claiming both would make the write path's branch order load-bearing.
        foreach (var mode in Enum.GetValues<WinhanceMode>())
        {
            var capabilities = ModeCapabilities.For(mode);

            (capabilities.AppliesToSystem && capabilities.AuthorsIntent)
                .Should().BeFalse($"{mode} cannot both apply and author");
        }
    }

    [Fact]
    public void EveryDeclaredMode_HasAnExplicitRow()
    {
        // The point of the throwing default arm: adding a WinhanceMode member must be a decision
        // about what it permits, not a silent inheritance of Normal's answers.
        var act = () => Enum.GetValues<WinhanceMode>()
            .Select(ModeCapabilities.For)
            .ToList();

        act.Should().NotThrow();
    }

    [Fact]
    public void AnUndeclaredMode_Throws()
    {
        var act = () => ModeCapabilities.For((WinhanceMode)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Capabilities_AreValueEqual_SoCallSitesCanCompareThemFreely()
    {
        ModeCapabilities.For(WinhanceMode.Normal)
            .Should().Be(ModeCapabilities.For(WinhanceMode.Normal));

        ModeCapabilities.For(WinhanceMode.Builder)
            .Should().NotBe(ModeCapabilities.For(WinhanceMode.Normal));
    }

    [Fact]
    public void NullModeService_AnswersAsNormal()
    {
        // No service means "not Builder", which is Normal's answer.
        ModeCapabilities.For(WinhanceMode.Normal)
            .Should().Be(((Winhance.Core.Features.Common.Interfaces.IApplicationModeService?)null).Capabilities());
    }
}
