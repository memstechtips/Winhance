using FluentAssertions;
using Winhance.Core.Features.Common.Helpers;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class AppDataPathsTests
{
    private const string AppDir = @"D:\Tools\Winhance";

    [Fact]
    public void PortableRootFor_MarkerBesideExe_IsDataFolderBesideExe()
    {
        AppDataPaths.PortableRootFor(AppDir, path => path == @"D:\Tools\Winhance\portable.marker")
            .Should().Be(@"D:\Tools\Winhance\Data");
    }

    [Fact]
    public void PortableRootFor_NoMarker_IsNull()
    {
        AppDataPaths.PortableRootFor(AppDir, _ => false).Should().BeNull();
    }

    [Fact]
    public void Root_Installed_IsWinhanceUnderTheBase()
    {
        AppDataPaths.Root(null, @"C:\ProgramData").Should().Be(@"C:\ProgramData\Winhance");
        AppDataPaths.Root(null, @"C:\Users\Test\AppData\Local").Should().Be(@"C:\Users\Test\AppData\Local\Winhance");
    }

    [Fact]
    public void Root_Portable_IgnoresTheBase()
    {
        AppDataPaths.Root(@"D:\Tools\Winhance\Data", @"C:\ProgramData").Should().Be(@"D:\Tools\Winhance\Data");
    }

    [Fact]
    public void Installed_MachineRootIsProgramDataWinhance()
    {
        // The test host has no portable.marker beside it.
        AppDataPaths.MachineRoot.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Winhance"));
    }
}
