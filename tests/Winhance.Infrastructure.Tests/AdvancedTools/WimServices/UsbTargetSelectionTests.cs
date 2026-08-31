using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.TestSupport;
using Xunit;
using Winhance.Core.Features.Common.Exceptions;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class UsbTargetSelectionTests
{
    private readonly Mock<IStorageEnumerator> _enumerator = new();
    private readonly Mock<IStorageOperations> _operations = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private readonly ManualTimeProvider _clock = new();

    public UsbTargetSelectionTests()
    {
        _fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                   .Returns((string[] parts) => string.Join("\\", parts));
    }

    [Fact]
    public void GetCandidateTargets_FixedDisk_IsNotOffered()
    {
        _enumerator.Setup(e => e.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Samsung SSD 990", 2_000_398_934_016L, "NVMe", IsSystemDisk: true),
            new RemovableDrive(2, "SanDisk Ultra",     61_530_439_680L, "USB",  IsSystemDisk: false),
        });

        var targets = BuildWriter().GetCandidateTargets();

        targets.Should().ContainSingle().Which.DiskNumber.Should().Be(2);
    }

    [Fact]
    public void GetCandidateTargets_SystemDiskOnUsb_IsRefused()
    {
        _enumerator.Setup(e => e.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Odd controller", 500_000_000_000L, "USB", IsSystemDisk: true),
        });

        var targets = BuildWriter().GetCandidateTargets();

        targets.Should().BeEmpty();
    }

    [Fact]
    public void GetCandidateTargets_NonUsbDataDisk_IsNotOffered()
    {
        _enumerator.Setup(e => e.GetDisks()).Returns(new[]
        {
            new RemovableDrive(1, "Seagate Barracuda", 4_000_787_030_016L, "SATA", IsSystemDisk: false),
        });

        var targets = BuildWriter().GetCandidateTargets();

        targets.Should().BeEmpty();
    }

    [Fact]
    public void Write_DiskIsNotACandidate_ErasesNothing()
    {
        _enumerator.Setup(e => e.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Samsung SSD 990", 2_000_398_934_016L, "NVMe", IsSystemDisk: true),
        });
        var systemDisk = new RemovableDrive(0, "Samsung SSD 990", 2_000_398_934_016L, "NVMe", IsSystemDisk: true);

        Action act = () => BuildWriter().Write(systemDisk, @"C:\work", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>();
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_PayloadOver32Gb_RefusesBeforeErasing()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(34_359_738_369L);

        Action act = () => BuildWriter().Write(stick, @"C:\work", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 GB*");
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_DriveTooSmall_RefusesBeforeErasing()
    {
        var stick = new RemovableDrive(2, "Tiny stick", 2_000_000_000L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(3_000_000_000L);

        Action act = () => BuildWriter().Write(stick, @"C:\work", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>();
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_OversizedEsd_RefusesRatherThanSplitting()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.esd"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.esd")).Returns(7_578_075_168L);
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\sources\install.wim")).Returns(false);

        Action act = () => BuildWriter().Write(stick, @"C:\work", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*WIM*");
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_ValidTarget_FormatsThenAssignsLetterThenCopies()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(3_000_000_000L);

        var order = new List<string>();
        _operations.Setup(o => o.Clear(2)).Callback(() => order.Add("Clear"));
        _operations.Setup(o => o.EnsureMbr(2)).Callback(() => order.Add("EnsureMbr"));
        _operations.Setup(o => o.CreateActiveFat32Partition(2)).Returns(1).Callback(() => order.Add("CreatePartition"));
        _operations.Setup(o => o.FormatFat32(2, 1, It.IsAny<string>())).Callback(() => order.Add("Format"));
        _operations.Setup(o => o.AssignDriveLetter(2, 1)).Returns('E').Callback(() => order.Add("AssignLetter"));
        var copier = new Mock<IMediaCopier>();
        copier.Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(),
                  It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
              .Callback(() => order.Add("Copy"));

        BuildWriter(copier.Object).Write(stick, @"C:\work", null, CancellationToken.None);

        order.Should().Equal("Clear", "EnsureMbr", "CreatePartition", "Format", "AssignLetter", "Copy");
        copier.Verify(c => c.CopyTree(@"C:\work", @"E:\", null,
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Write_ImageNeedsSplitting_CopiesEverythingElseThenSplitsOntoTheMedia()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\setup.exe", @"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\setup.exe")).Returns(1_000_000L);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(7_578_075_168L);
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\sources\install.wim")).Returns(true);
        _operations.Setup(o => o.CreateActiveFat32Partition(2)).Returns(1);
        _operations.Setup(o => o.AssignDriveLetter(2, 1)).Returns('E');

        Func<string, bool>? skip = null;
        var copier = new Mock<IMediaCopier>();
        copier.Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(),
                  It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
              .Callback<string, string, Func<string, bool>?, IProgress<TaskProgressDetail>?, CancellationToken>(
                  (_, _, predicate, _, _) => skip = predicate);

        var dism = new Mock<IDismProcessRunner>();
        dism.Setup(d => d.RunProcessWithProgressAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty));

        BuildWriter(copier.Object, dism.Object).Write(stick, @"C:\work", null, CancellationToken.None);

        skip.Should().NotBeNull();
        skip!(@"C:\work\sources\install.wim").Should().BeTrue();
        skip(@"C:\work\setup.exe").Should().BeFalse();
        dism.Verify(d => d.RunProcessWithProgressAsync(
            "dism.exe",
            It.Is<string>(a => a.Contains("/Split-Image") && a.Contains(@"E:\sources\install.swm") && a.Contains("/FileSize:3800")),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Write_SplittingTheImage_AppendsTheWriteSpeedToDismsBar()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(7_578_075_168L);
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\sources\install.wim")).Returns(true);
        _operations.Setup(o => o.CreateActiveFat32Partition(2)).Returns(1);
        _operations.Setup(o => o.AssignDriveLetter(2, 1)).Returns('E');

        var dism = new Mock<IDismProcessRunner>();
        dism.Setup(d => d.RunProcessWithProgressAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, IProgress<TaskProgressDetail>, CancellationToken>((_, _, p, _) =>
            {
                p.Report(new TaskProgressDetail { Progress = 10, TerminalOutput = "[=  10.0%  ]", IsProgressIndicator = true });
                _clock.Advance(TimeSpan.FromSeconds(1));
                p.Report(new TaskProgressDetail { Progress = 60, TerminalOutput = "[====  60.0%  ]", IsProgressIndicator = true });
            })
            .ReturnsAsync((0, string.Empty));

        var reports = new List<TaskProgressDetail>();
        BuildWriter(Mock.Of<IMediaCopier>(), dism.Object)
            .Write(stick, @"C:\work", new SynchronousProgress<TaskProgressDetail>(reports.Add), CancellationToken.None);

        var bars = reports.Where(r => r.Progress.HasValue).ToList();
        bars.Should().HaveCount(2);
        bars[0].TerminalOutput.Should().Be("[=  10.0%  ]");
        bars[1].TerminalOutput.Should().StartWith("[====  60.0%  ] ").And.EndWith(" MB/s");
    }

    [Fact]
    public void Write_OversizedFileBesideTheWim_RefusesBeforeErasing()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim", @"C:\work\sources\install.esd"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(3_000_000_000L);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.esd")).Returns(7_578_075_168L);
        _fileSystem.Setup(fs => fs.GetFileName(@"C:\work\sources\install.esd")).Returns("install.esd");

        Action act = () => BuildWriter().Write(stick, @"C:\work", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*install.esd*");
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_TargetHoldsTheWorkingFolder_RefusesBeforeErasing()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _enumerator.Setup(e => e.GetDriveLetters(2)).Returns(['F']);
        _fileSystem.Setup(fs => fs.GetPathRoot(@"F:\work")).Returns(@"F:\");

        Action act = () => BuildWriter().Write(stick, @"F:\work", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SanDisk Ultra*");
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_StepFailsAfterTheErase_ReportsTheDriveAsErased()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(3_000_000_000L);
        _operations.Setup(o => o.FormatFat32(2, It.IsAny<int>(), It.IsAny<string>()))
                   .Throws(new InvalidOperationException("Could not format partition 1 on disk 2: The device is not ready."));

        Action act = () => BuildWriter().Write(stick, @"C:\work", null, CancellationToken.None);

        var erased = act.Should().Throw<UsbMediaErasedException>().Which;
        erased.Target.Should().Be(stick);
        erased.WasCancelled.Should().BeFalse();
        erased.Message.Should().Contain("not ready");
        _operations.Verify(o => o.Clear(2), Times.Once);
    }

    [Fact]
    public void Write_CancelledDuringTheCopy_ReportsTheDriveAsErased()
    {
        var stick = new RemovableDrive(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false);
        _enumerator.Setup(e => e.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns([@"C:\work\sources\install.wim"]);
        _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\sources\install.wim")).Returns(3_000_000_000L);
        _operations.Setup(o => o.CreateActiveFat32Partition(2)).Returns(1);
        _operations.Setup(o => o.AssignDriveLetter(2, 1)).Returns('E');
        var copier = new Mock<IMediaCopier>();
        copier.Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(),
                  It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
              .Throws(new OperationCanceledException());

        Action act = () => BuildWriter(copier.Object).Write(stick, @"C:\work", null, CancellationToken.None);

        act.Should().Throw<UsbMediaErasedException>().Which.WasCancelled.Should().BeTrue();
    }

    private StorageApiUsbMediaWriter BuildWriter(
        IMediaCopier? mediaCopier = null,
        IDismProcessRunner? dismProcessRunner = null)
    {
        return new StorageApiUsbMediaWriter(
            _enumerator.Object,
            _operations.Object,
            mediaCopier ?? Mock.Of<IMediaCopier>(),
            dismProcessRunner ?? Mock.Of<IDismProcessRunner>(),
            _fileSystem.Object,
            Mock.Of<ILocalizationService>(),
            Mock.Of<ILogService>(),
            _clock);
    }
}
