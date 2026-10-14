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

    [Fact]
    public void CopyInstalledData_FirstRun_CopiesSettingsBackupsAndHistory_AndKeepsTheOriginals()
    {
        using var dirs = new TempDirs();
        dirs.Write(@"user\Config\UserPreferences.json", "{}");
        dirs.Write(@"user\Backup\PowerPlans\plan.pow", "p");
        dirs.Write(@"user\Cache\winget.json", "c");
        dirs.Write(@"machine\ChangeHistory.txt", "h");
        dirs.Write(@"machine\Logs\old.log", "l");

        var copied = AppDataPaths.CopyInstalledData(dirs.Path("data"), dirs.Path("user"), dirs.Path("machine"));

        copied.Should().HaveCount(3);
        File.ReadAllText(dirs.Path(@"data\Config\UserPreferences.json")).Should().Be("{}");
        File.ReadAllText(dirs.Path(@"data\Backup\PowerPlans\plan.pow")).Should().Be("p");
        File.ReadAllText(dirs.Path(@"data\ChangeHistory.txt")).Should().Be("h");
        Directory.Exists(dirs.Path(@"data\Cache")).Should().BeFalse();
        Directory.Exists(dirs.Path(@"data\Logs")).Should().BeFalse();
        File.Exists(dirs.Path(@"user\Config\UserPreferences.json")).Should().BeTrue();
    }

    [Fact]
    public void CopyInstalledData_PortableAlreadyHasIt_LeavesItAlone()
    {
        using var dirs = new TempDirs();
        dirs.Write(@"user\Config\UserPreferences.json", "installed");
        dirs.Write(@"data\Config\UserPreferences.json", "portable");

        AppDataPaths.CopyInstalledData(dirs.Path("data"), dirs.Path("user"), dirs.Path("machine")).Should().BeEmpty();

        File.ReadAllText(dirs.Path(@"data\Config\UserPreferences.json")).Should().Be("portable");
    }

    [Fact]
    public void CopyInstalledData_LeftoverFromACopyCutShort_IsRedone()
    {
        using var dirs = new TempDirs();
        dirs.Write(@"user\Backup\a.winhance", "a");
        dirs.Write(@"data\Backup.copying\half.tmp", "x");

        AppDataPaths.CopyInstalledData(dirs.Path("data"), dirs.Path("user"), dirs.Path("machine"));

        Directory.GetFiles(dirs.Path(@"data\Backup")).Select(Path.GetFileName).Should().Equal("a.winhance");
        Directory.Exists(dirs.Path(@"data\Backup.copying")).Should().BeFalse();
    }

    private sealed class TempDirs : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wh-portable-" + Guid.NewGuid().ToString("N"));

        public string Path(string relative) =>
            System.IO.Path.Combine(_root, relative.Replace('\\', System.IO.Path.DirectorySeparatorChar));

        public void Write(string relative, string text)
        {
            var path = Path(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
