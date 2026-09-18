using FluentAssertions;
using Winhance.Core.Features.Autounattend;
using Xunit;

namespace Winhance.Core.Tests.Autounattend;

public class AccountPasswordsTests
{
    [Fact]
    public void The_documented_microsoft_examples_decode_back_to_the_password()
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
    public void An_unreadable_value_is_no_password(string? value)
    {
        AccountPasswords.TryDeobscure(value, "Password", out var password).Should().BeFalse();
        password.Should().BeEmpty();
    }

    [Fact]
    public void Base64_that_does_not_end_in_the_element_name_is_no_password()
    {
        var obscuredForAnotherElement = AccountPasswords.Obscure("hunter2", "Value");

        AccountPasswords.TryDeobscure(obscuredForAnotherElement, "Password", out var password).Should().BeFalse();
        password.Should().BeEmpty();
    }

    [Fact]
    public void A_password_outside_ascii_round_trips()
    {
        const string typed = "caf\u00e9 \u00fcber";

        AccountPasswords.TryDeobscure(AccountPasswords.Obscure(typed, "Password"), "Password", out var read)
            .Should().BeTrue();
        read.Should().Be(typed);
    }
}
