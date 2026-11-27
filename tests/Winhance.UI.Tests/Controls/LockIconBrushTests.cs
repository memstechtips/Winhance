using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// The locked-setting icon is orange (#FFA500) in Light and Dark. WinUI's caution brush is yellow in Dark, so the
// colour lives in a named brush in each theme dictionary rather than borrowing a system status brush.
public class LockIconBrushTests
{
    private const string BrushKey = "LockedSettingIconBrush";
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Load(string relative) =>
        XDocument.Load(Path.Combine(RepoPaths.SolutionDir(), relative));

    [Fact]
    public void LockIcon_UsesTheLockedSettingBrush()
    {
        var icon = Load("src/Winhance.UI/Features/Common/Controls/SettingsCardItem.xaml")
            .Descendants()
            .Single(e => (string?)e.Attribute(Xaml + "Name") == "UnlockPrivacyIcon");

        ((string?)icon.Attribute("Foreground")).Should().Be("{ThemeResource " + BrushKey + "}");
    }

    [Theory]
    [InlineData("Light", "#FFA500")]
    [InlineData("Dark", "#FFA500")]
    [InlineData("HighContrast", null)]
    public void LockedSettingBrush_IsDefinedInEveryTheme(string theme, string? colour)
    {
        var dictionary = Load("src/Winhance.UI/Features/Common/Resources/BadgeStyles.xaml")
            .Descendants(Presentation + "ResourceDictionary")
            .Single(d => (string?)d.Attribute(Xaml + "Key") == theme);

        var brush = dictionary.Elements(Presentation + "SolidColorBrush")
            .SingleOrDefault(b => (string?)b.Attribute(Xaml + "Key") == BrushKey);

        brush.Should().NotBeNull();
        if (colour is not null)
            ((string?)brush!.Attribute("Color")).Should().Be(colour);
    }
}
