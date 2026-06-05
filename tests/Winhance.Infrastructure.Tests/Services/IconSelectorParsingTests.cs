using FluentAssertions;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// Covers <see cref="AppIconResolver.SplitIconSelector"/> — the parser that turns an
/// IconSources entry like "shell32.dll,#512" into a (path, PrivateExtractIcons-selector) pair.
/// This is the one piece of the icon-index work that runs without Windows (no GDI/WinRT).
/// </summary>
public class IconSelectorParsingTests
{
    [Theory]
    // Resource-ID form (#N) -> negated selector (PrivateExtractIcons: negative == resource ID).
    [InlineData(@"%SystemRoot%\System32\shell32.dll,#512", @"%SystemRoot%\System32\shell32.dll", -512)]
    [InlineData("scrptadm.dll,#7", "scrptadm.dll", -7)]
    [InlineData(@"%SystemRoot%\System32\wmploc.dll,#102", @"%SystemRoot%\System32\wmploc.dll", -102)]
    // Plain index form (N) -> positive selector (zero-based position).
    [InlineData("imageres.dll,5", "imageres.dll", 5)]
    [InlineData("imageres.dll,0", "imageres.dll", 0)]
    public void SplitIconSelector_ParsesSelector(string input, string expectedPath, int expectedSelector)
    {
        var (path, selector) = AppIconResolver.SplitIconSelector(input);

        path.Should().Be(expectedPath);
        selector.Should().Be(expectedSelector);
    }

    [Theory]
    [InlineData(@"%SystemRoot%\System32\notepad.exe")]              // no comma / no selector
    [InlineData(@"C:\Program Files\Internet Explorer\iexplore.exe")]
    [InlineData(@"C:\a,b\icon.png")]                               // comma is in the path, suffix not an integer
    [InlineData("file.dll,")]                                      // trailing comma, empty suffix
    [InlineData(",5")]                                             // leading comma (no path)
    [InlineData("file.dll,#abc")]                                  // non-integer resource id
    [InlineData("file.dll,#")]                                     // empty resource id
    [InlineData("file.dll,-5")]                                    // negative index rejected
    public void SplitIconSelector_NoValidSelector_ReturnsSourceUnchanged(string input)
    {
        var (path, selector) = AppIconResolver.SplitIconSelector(input);

        path.Should().Be(input);
        selector.Should().BeNull();
    }
}
