using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class PortableDataCopyTests
{
    private readonly PortableDataCopy _copy = new(new FileSystemService(Mock.Of<ILogService>()));

    [Fact]
    public void CopyInstalledData_FirstRun_CopiesSettingsBackupsAndHistory_AndKeepsTheOriginals()
    {
        using var dirs = new TempDirs();
        dirs.Write(@"user\Config\UserPreferences.json", "{}");
        dirs.Write(@"user\Backup\PowerPlans\plan.pow", "p");
        dirs.Write(@"user\Cache\winget.json", "c");
        dirs.Write(@"machine\ChangeHistory.txt", "h");
        dirs.Write(@"machine\Logs\old.log", "l");

        var copied = _copy.CopyInstalledData(dirs.Path("data"), dirs.Path("user"), dirs.Path("machine"));

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

        _copy.CopyInstalledData(dirs.Path("data"), dirs.Path("user"), dirs.Path("machine")).Should().BeEmpty();

        File.ReadAllText(dirs.Path(@"data\Config\UserPreferences.json")).Should().Be("portable");
    }

    [Fact]
    public void CopyInstalledData_LeftoverFromACopyCutShort_IsRedone()
    {
        using var dirs = new TempDirs();
        dirs.Write(@"user\Backup\a.winhance", "a");
        dirs.Write(@"data\Backup.copying\half.tmp", "x");

        _copy.CopyInstalledData(dirs.Path("data"), dirs.Path("user"), dirs.Path("machine"));

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
