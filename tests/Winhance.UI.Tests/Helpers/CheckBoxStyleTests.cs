using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// App.OnLaunched adds an implicit CheckBox style that pins AnimatedIcon.State so a hovered card cannot draw a ghost
// check into an unticked box. A CheckBox with its own Style skips that implicit style.
public class CheckBoxStyleTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static IEnumerable<string> XamlFiles() => RepoPaths.SourceFiles("*.xaml");

    // A XAML Setter for AnimatedIcon.State crashes the app on start with "Failed to assign to property
    // AnimatedIcon.FallbackIconSource"; the style has to be built in code.
    [Fact]
    public void XamlFiles_Always_HaveNoSetterTargetingAnimatedIconState()
    {
        var offenders = XamlFiles()
            .SelectMany(f => XDocument.Load(f).Descendants(Presentation + "Setter")
                .Where(s => ((string?)s.Attribute("Property"))?.EndsWith("AnimatedIcon.State", StringComparison.Ordinal) == true)
                .Select(_ => Path.GetFileName(f)))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void XamlFiles_Always_HaveNoCheckBoxSettingItsOwnStyle()
    {
        var offenders = XamlFiles()
            .SelectMany(f => XDocument.Load(f).Descendants(Presentation + "CheckBox")
                .Where(c => c.Attribute("Style") is not null)
                .Select(_ => Path.GetFileName(f)))
            .ToList();

        offenders.Should().BeEmpty();
    }
}
