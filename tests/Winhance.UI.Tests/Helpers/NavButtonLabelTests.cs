using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class NavButtonLabelTests
{
    // A break inside a word ("Einstellu-ngen") reads as two words; a word too long for the label shrinks instead.
    [Fact]
    public void Label_WrapsOnlyBetweenWords()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Controls", "NavButton.xaml"));
        var label = Regex.Match(xaml, "<TextBlock x:Name=\"ButtonText\"[^>]*>").Value;

        label.Should().Contain("TextWrapping=\"WrapWholeWords\"").And.Contain("Loaded=\"ButtonText_Loaded\"");
    }

    [Theory]
    [InlineData("Einstellungen", 13)]
    [InlineData("Software & Apps", 8)]
    [InlineData("Software y Aplicaciones", 12)]
    [InlineData("", 0)]
    public void WidestWord_MeasuresEachWordOnItsOwn(string text, double widest) =>
        FitToWidth.WidestWord(text, word => word.Length).Should().Be(widest);
}
