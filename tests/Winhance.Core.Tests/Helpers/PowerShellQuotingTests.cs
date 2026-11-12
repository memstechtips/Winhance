using FluentAssertions;
using Winhance.Core.Features.Common.Helpers;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class PowerShellQuotingTests
{
    [Theory]
    [InlineData("it's", "it''s")]
    [InlineData("l\u2019a", "l\u2019\u2019a")]
    [InlineData("plain", "plain")]
    public void Escape_DoublesEverySingleQuote(string input, string expected)
        => PowerShellQuoting.Escape(input).Should().Be(expected);

    [Theory]
    [InlineData("it's")]
    [InlineData("it''s")]
    [InlineData("'")]
    [InlineData("\u2018x\u201B")]
    public void Unescape_UndoesEscape(string original)
        => PowerShellQuoting.Unescape(PowerShellQuoting.Escape(original)!).Should().Be(original);
}
