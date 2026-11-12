using FluentAssertions;
using Winhance.Core.Features.Autounattend;
using Xunit;

namespace Winhance.Core.Tests.Autounattend;

public class AccountPasswordsTests
{
    [Fact]
    public void TryDeobscure_DocumentedMicrosoftExamples_DecodeBackToThePassword()
    {
        AccountPasswords.TryDeobscure("cAB3AFAAYQBzAHMAdwBvAHIAZAA=", "Password", out var two).Should().BeTrue();
        two.Should().Be("pw");

        AccountPasswords.TryDeobscure("cABhAHMAcwB3AG8AcgBkAFAAYQBzAHMAdwBvAHIAZAA=", "Password", out var eight).Should().BeTrue();
        eight.Should().Be("password");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hunter2")]
    [InlineData("!!not base64!!")]
    public void TryDeobscure_UnreadableValue_ReturnsNoPassword(string? value)
    {
        AccountPasswords.TryDeobscure(value, "Password", out var password).Should().BeFalse();
        password.Should().BeEmpty();
    }

    [Fact]
    public void TryDeobscure_Base64NotEndingInTheElementName_ReturnsNoPassword()
    {
        var obscuredForAnotherElement = AccountPasswords.Obscure("hunter2", "Value");

        AccountPasswords.TryDeobscure(obscuredForAnotherElement, "Password", out var password).Should().BeFalse();
        password.Should().BeEmpty();
    }

    [Fact]
    public void TryDeobscure_PasswordOutsideAscii_RoundTrips()
    {
        const string typed = "caf\u00e9 \u00fcber";

        AccountPasswords.TryDeobscure(AccountPasswords.Obscure(typed, "Password"), "Password", out var read)
            .Should().BeTrue();
        read.Should().Be(typed);
    }
}
