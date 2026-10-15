using FluentAssertions;
using Winhance.UI.Features.Common.Controls;
using Xunit;

namespace Winhance.UI.Tests.Controls;

public class SponsorLogoTests
{
    [Theory]
    [InlineData("imperium", "I")]
    [InlineData("  acme", "A")]
    [InlineData("go winhance! kracken them all", "G")]
    public void Initial_IsTheFirstLetterUppercased(string name, string expected)
    {
        SponsorLogo.Initial(name).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Initial_NoName_IsAQuestionMark(string? name)
    {
        SponsorLogo.Initial(name).Should().Be("?");
    }
}
