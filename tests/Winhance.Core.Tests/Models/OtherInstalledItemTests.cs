using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class OtherInstalledItemTests
{
    [Fact]
    public void A_program_uninstalls_through_its_exact_key_and_a_store_app_through_its_package()
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
    public void A_program_winget_knows_uninstalls_through_winget_first_and_keeps_its_key_as_the_fallback()
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
    public void Chocolatey_uninstalls_what_it_installed()
    {
        var definition = OtherInstalledItem.Create(new InstalledApp("reg:machine:np", "Notepad++", InstalledAppKind.Program)
        {
            RegistryKeyName = "Notepad++", ChocoId = "notepadplusplus", InstalledByChocolatey = true,
        });

        definition.DetectedVia.Should().Be(DetectionSource.Chocolatey);
        definition.ChocoPackageId.Should().Be("notepadplusplus");
    }

    [Fact]
    public void The_publisher_stands_in_for_the_description()
    {
        var definition = OtherInstalledItem.Create(new InstalledApp("reg:machine:x", "Tool", InstalledAppKind.Program) { Publisher = "Contoso" });

        definition.Description.Should().Be("Contoso");
    }

    [Fact]
    public void A_file_saves_the_app_by_its_winget_id_then_its_Store_id()
    {
        var app = new InstalledApp("reg:machine:x", "Tool", InstalledAppKind.Program);

        OtherInstalledItem.PackageId(app with { WinGetId = "Contoso.Tool", MsStoreId = "9N1" }).Should().Be("winget-Contoso.Tool");
        OtherInstalledItem.PackageId(app with { MsStoreId = "9N1" }).Should().Be("msstore-9N1");
        OtherInstalledItem.PackageId(app).Should().BeNull();
    }
}
