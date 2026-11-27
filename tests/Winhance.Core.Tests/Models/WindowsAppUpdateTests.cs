using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class WindowsAppUpdateTests
{
    private static InstalledApp App(string? installed, string? available, PackageSources via, string key = "appx:Terminal") =>
        new(key, "Terminal", InstalledAppKind.StoreApp) { Version = installed, AvailableVersion = available, UpdateVia = via };

    [Fact]
    public void Applies_WingetSourceNewerVersion_IsAnUpdate() =>
        WindowsAppUpdate.Applies(App("1.24.11911.0", "1.25.2733.0", PackageSources.WinGet)).Should().BeTrue();

    [Fact]
    public void Applies_StoreCatalogNewerVersion_IsAnUpdate() =>
        WindowsAppUpdate.Applies(App("2021.2502.2.0", "2021.2607.0.0", PackageSources.MsStore)).Should().BeTrue();

    [Fact]
    public void Applies_StoreCatalogEqualVersion_IsNotAnUpdate() =>
        WindowsAppUpdate.Applies(App("2021.2607.0.0", "2021.2607.0.0", PackageSources.MsStore)).Should().BeFalse();

    [Theory]
    [InlineData("1.25.2733.0", "1.25.2733.0")]
    [InlineData("1.25.2733.0", "1.24.11911.0")]
    public void Applies_EqualOrOlderAvailable_IsNotAnUpdate(string installed, string available) =>
        WindowsAppUpdate.Applies(App(installed, available, PackageSources.WinGet)).Should().BeFalse();

    [Theory]
    [InlineData("1.0.0.1")]
    [InlineData("0.0.0.0")]
    public void Applies_PlaceholderInstalledVersion_IsNotAnUpdate(string installed) =>
        WindowsAppUpdate.Applies(App(installed, "26276.513.5241.9675", PackageSources.WinGet)).Should().BeFalse();

    [Fact]
    public void Applies_NoUpdateOrUnreadableVersion_IsNotAnUpdate()
    {
        WindowsAppUpdate.Applies(null).Should().BeFalse();
        WindowsAppUpdate.Applies(App("1.0", null, PackageSources.None)).Should().BeFalse();
        WindowsAppUpdate.Applies(App("Unknown", "2.0", PackageSources.WinGet)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pick_EdgeStubAndWin32Build_TakesTheBuildThatHasTheUpdate(bool stubFirst)
    {
        var stub = App("152.0.4191.66", null, PackageSources.None, "appx:Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe");
        var win32 = App("155.0.4283.62", "156.0.4314.58", PackageSources.WinGet, "reg:machine:Microsoft Edge");

        var picked = WindowsAppUpdate.Pick(stubFirst ? [stub, win32] : [win32, stub]);

        picked.Should().BeSameAs(win32);
    }

    [Fact]
    public void Pick_NoUpdateAnywhere_TakesTheFirstEntry()
    {
        var first = App("1.0", null, PackageSources.None, "appx:A");

        WindowsAppUpdate.Pick([first, App("2.0", null, PackageSources.None, "appx:B")]).Should().BeSameAs(first);
        WindowsAppUpdate.Pick([]).Should().BeNull();
    }
}
