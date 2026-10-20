using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class MediaCopierTests
{
    private readonly Mock<IFileCopyNative> _native = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly ManualTimeProvider _clock = new();

    public MediaCopierTests()
    {
        _fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                   .Returns((string[] parts) => string.Join("\\", parts));
        _fileSystem.Setup(fs => fs.GetFileName(It.IsAny<string>()))
                   .Returns((string path) => path.Split('\\')[^1]);
        _fileSystem.Setup(fs => fs.GetDirectories(It.IsAny<string>()))
                   .Returns(Array.Empty<string>());
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
                     .Returns((string key, object[] args) => key);
    }

    [Fact]
    public void CopyTree_Always_ReportsBytesNotFileCount()
    {
        GivenOneFileOf(4_000);
        _native.Setup(n => n.CopyWithProgress(It.IsAny<string>(), It.IsAny<string>(),
                   It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()))
               .Callback<string, string, Action<long>, CancellationToken>(
                   (_, _, cb, _) => { cb(1_000); cb(4_000); });

        var reports = new List<TaskProgressDetail>();
        BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, new SynchronousProgress<TaskProgressDetail>(reports.Add), CancellationToken.None);

        reports.Should().Contain(r => r.Progress == 25);
        reports.Should().Contain(r => r.Progress == 100);
    }

    [Fact]
    public void CopyTree_PercentageUnchanged_ReportsOnlyOnce()
    {
        GivenOneFileOf(4_000);
        _native.Setup(n => n.CopyWithProgress(It.IsAny<string>(), It.IsAny<string>(),
                   It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()))
               .Callback<string, string, Action<long>, CancellationToken>(
                   (_, _, cb, _) => { cb(1_000); cb(1_001); cb(4_000); });

        var reports = new List<TaskProgressDetail>();
        BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, new SynchronousProgress<TaskProgressDetail>(reports.Add), CancellationToken.None);

        reports.Select(r => r.Progress).Should().Equal(25d, 100d);
    }

    [Fact]
    public void CopyTree_AfterASecond_ShowsTheWriteSpeedNextToTheFile()
    {
        const long mebibyte = 1024 * 1024;
        GivenOneFileOf(8 * mebibyte);
        _native.Setup(n => n.CopyWithProgress(It.IsAny<string>(), It.IsAny<string>(),
                   It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()))
               .Callback<string, string, Action<long>, CancellationToken>((_, _, cb, _) =>
               {
                   cb(2 * mebibyte);
                   _clock.Advance(TimeSpan.FromSeconds(1));
                   cb(4 * mebibyte);
               });

        var reports = new List<TaskProgressDetail>();
        BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, new SynchronousProgress<TaskProgressDetail>(reports.Add), CancellationToken.None);

        // Four mebibytes had moved by the time the clock reached one second.
        reports.Select(r => r.TerminalOutput).Should().Equal("setup.exe", $"setup.exe ({4.0:F1} MB/s)");
    }

    [Fact]
    public void CopyTree_CancelledBeforeStarting_ThrowsBeforeTouchingTheDestination()
    {
        GivenOneFileOf(4_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Action act = () => BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        _native.Verify(n => n.CopyWithProgress(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // A copy that ignored the token would run a 7 GB install.wim to the end under a dead Cancel
    // button; only the native layer can stop it mid-file, so it must be handed the caller's token.
    [Fact]
    public void CopyTree_CancelledDuringAFile_HandsTheCallersTokenToTheNativeCopy()
    {
        GivenOneFileOf(4_000);
        using var cts = new CancellationTokenSource();
        var seen = CancellationToken.None;
        _native.Setup(n => n.CopyWithProgress(It.IsAny<string>(), It.IsAny<string>(),
                   It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()))
               .Callback<string, string, Action<long>, CancellationToken>((_, _, cb, ct) =>
               {
                   seen = ct;
                   cb(1_000);
                   cts.Cancel();
                   throw new OperationCanceledException(ct);
               });

        Action act = () => BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        seen.Should().Be(cts.Token);
    }

    [Fact]
    public void CopyTree_EachCopiedFile_HasItsReadOnlyAttributeCleared()
    {
        GivenOneFileOf(4_000);

        BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, null, CancellationToken.None);

        _fileSystem.Verify(fs => fs.SetFileAttributes(@"C:\work\setup.exe", FileAttributes.Normal), Times.Once);
    }

    [Fact]
    public void CopyTree_NestedFile_CopiesIntoTheMatchingSubdirectory()
    {
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\", "*", SearchOption.AllDirectories))
                   .Returns([@"E:\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\")).Returns(Array.Empty<string>());
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\sources")).Returns([@"E:\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetDirectories(@"E:\")).Returns([@"E:\sources"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"E:\sources\install.wim")).Returns(7_578_075_168L);

        BuildCopier().CopyTree(@"E:\", @"C:\work", null, null, 0, null, CancellationToken.None);

        _native.Verify(n => n.CopyWithProgress(
            @"E:\sources\install.wim",
            @"C:\work\sources\install.wim",
            It.IsAny<Action<long>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CopyTree_SkipPredicateMatches_DoesNotCopyThatFile()
    {
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\", "*", SearchOption.AllDirectories))
                   .Returns([@"E:\setup.exe", @"E:\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\")).Returns([@"E:\setup.exe", @"E:\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"E:\setup.exe")).Returns(1_000L);
        _fileSystem.Setup(fs => fs.GetFileSize(@"E:\install.wim")).Returns(7_000_000_000L);

        BuildCopier().CopyTree(@"E:\", @"C:\work", path => path.EndsWith("install.wim"), null, 0, null, CancellationToken.None);

        _native.Verify(n => n.CopyWithProgress(@"E:\setup.exe", It.IsAny<string>(),
            It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()), Times.Once);
        _native.Verify(n => n.CopyWithProgress(@"E:\install.wim", It.IsAny<string>(),
            It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CopyTree_SkipPredicateMatchesAFolder_NeverCreatesOrEntersIt()
    {
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\")).Returns(Array.Empty<string>());
        _fileSystem.Setup(fs => fs.GetDirectories(@"E:\")).Returns([@"E:\support", @"E:\boot"]);
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\boot")).Returns([@"E:\boot\bcd"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"E:\boot\bcd")).Returns(16_384L);

        BuildCopier().CopyTree(@"E:\", @"C:\work", path => path.EndsWith("support"), 16_384L, 0, null, CancellationToken.None);

        _fileSystem.Verify(fs => fs.GetFiles(@"E:\support"), Times.Never);
        _fileSystem.Verify(fs => fs.CreateDirectory(@"C:\work\support"), Times.Never);
        _fileSystem.Verify(fs => fs.CreateDirectory(@"C:\work\boot"), Times.Once);
    }

    [Fact]
    public void CopyTree_BytesBefore_CarriesOnThePreviousPassesBar()
    {
        GivenOneFileOf(4_000);
        _native.Setup(n => n.CopyWithProgress(It.IsAny<string>(), It.IsAny<string>(),
                   It.IsAny<Action<long>>(), It.IsAny<CancellationToken>()))
               .Callback<string, string, Action<long>, CancellationToken>((_, _, cb, _) => cb(4_000));

        var reports = new List<TaskProgressDetail>();
        BuildCopier().CopyTree(@"E:\", @"C:\work", null, 8_000L, 4_000L, new SynchronousProgress<TaskProgressDetail>(reports.Add), CancellationToken.None);

        reports.Select(r => r.Progress).Should().Equal(100d);
    }

    private void GivenOneFileOf(long sizeBytes)
    {
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\", "*", SearchOption.AllDirectories))
                   .Returns([@"E:\setup.exe"]);
        _fileSystem.Setup(fs => fs.GetFiles(@"E:\")).Returns([@"E:\setup.exe"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"E:\setup.exe")).Returns(sizeBytes);
    }

    private MediaCopier BuildCopier()
    {
        return new MediaCopier(
            _native.Object,
            _fileSystem.Object,
            _localization.Object,
            Mock.Of<ILogService>(),
            _clock);
    }
}
