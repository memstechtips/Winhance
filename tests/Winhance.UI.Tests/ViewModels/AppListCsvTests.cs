using FluentAssertions;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AppListCsvTests
{
    private static readonly (string Header, Func<string, string> Value)[] OneColumn = [("Name", value => value)];

    [Fact]
    public void Build_WritesTheHeaderThenOneLinePerRow()
    {
        AppListCsv.Build(OneColumn, ["7-Zip", "VLC"]).Should().Be("Name\r\n7-Zip\r\nVLC\r\n");
    }

    [Theory]
    [InlineData("Paint, classic", "\"Paint, classic\"")]
    [InlineData("The \"new\" Outlook", "\"The \"\"new\"\" Outlook\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    public void Build_QuotesValuesThatWouldBreakTheRow(string value, string expected)
    {
        AppListCsv.Build(OneColumn, [value]).Should().Be("Name\r\n" + expected + "\r\n");
    }
}
