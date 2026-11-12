using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class OtherInstalledItemTests
{
    [Fact]
    public void Create_ProgramAndStoreApp_UninstallThroughKeyAndPackage()
    {
        var program = OtherInstalledItem.Create(new InstalledApp("reg:machine:{GUID}", "App", InstalledAppKind.Program) { RegistryKeyName = "{GUID}" });
        var store = OtherInstalledItem.Create(new InstalledApp("appx:Paint", "Paint", InstalledAppKind.StoreApp) { PackageName = "Microsoft.Paint" });

        program.RegistrySubKeyName.Should().Be("{GUID}");
        program.GroupName.Should().Be(OtherInstalledItem.GroupName);
        program.IsInstalled.Should().BeTrue();
        program.DetectedVia.Should().Be(DetectionSource.Registry);
        store.AppxPackageName.Should().Equal("Microsoft.Paint");
        store.DetectedVia.Should().Be(DetectionSource.AppX);
    }

    [Fact]
    public void Create_ProgramKnownToWinGet_UninstallsThroughWinGetWithKeyAsFallback()
    {
        var definition = OtherInstalledItem.Create(new InstalledApp("reg:user:auto", "Autologon", InstalledAppKind.Program)
        {
            RegistryKeyName = "Microsoft.Sysinternals.Autologon_Microsoft.Winget.Source_8wekyb3d8bbwe",
            WinGetId = "Microsoft.Sysinternals.Autologon",
        });

        definition.DetectedVia.Should().Be(DetectionSource.WinGet);
        definition.WinGetPackageId.Should().Equal("Microsoft.Sysinternals.Autologon");
        definition.RegistrySubKeyName.Should().Be("Microsoft.Sysinternals.Autologon_Microsoft.Winget.Source_8wekyb3d8bbwe");
    }

    [Fact]
    public void Create_ChocolateyInstalledProgram_UninstallsThroughChocolatey()
    {
        var definition = OtherInstalledItem.Create(new InstalledApp("reg:machine:np", "Notepad++", InstalledAppKind.Program)
        {
            RegistryKeyName = "Notepad++", ChocoId = "notepadplusplus", InstalledByChocolatey = true,
        });

        definition.DetectedVia.Should().Be(DetectionSource.Chocolatey);
        definition.ChocoPackageId.Should().Be("notepadplusplus");
    }

    [Fact]
    public void Create_AppWithPublisher_UsesPublisherAsDescription()
    {
        var definition = OtherInstalledItem.Create(new InstalledApp("reg:machine:x", "Tool", InstalledAppKind.Program) { Publisher = "Contoso" });

        definition.Description.Should().Be("Contoso");
    }

    [Fact]
    public void PackageId_AppWithWinGetOrStoreId_PrefersWinGetThenStore()
    {
        var app = new InstalledApp("reg:machine:x", "Tool", InstalledAppKind.Program);

        OtherInstalledItem.PackageId(app with { WinGetId = "Contoso.Tool", MsStoreId = "9N1" }).Should().Be("winget-Contoso.Tool");
        OtherInstalledItem.PackageId(app with { MsStoreId = "9N1" }).Should().Be("msstore-9N1");
        OtherInstalledItem.PackageId(app).Should().BeNull();
    }
}
