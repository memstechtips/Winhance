using FluentAssertions;
using Moq;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;
using Winhance.Core.Features.Common.Exceptions;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class UsbTargetSelectionTests
{
    private readonly Mock<IStorageOperations> _operations = new();
    private static readonly RemovableDrive Stick = new(2, "SanDisk Ultra", 61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true);

    private readonly Mock<IFileSystemService> _fileSystem = new();

    public UsbTargetSelectionTests()
    {
        _fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                   .Returns((string[] parts) => string.Join("\\", parts));
    }

    [Fact]
    public void GetCandidateTargets_FixedDisk_IsNotOffered()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Samsung SSD 990", 2_000_398_934_016L, "NVMe", IsSystemDisk: true, HasRemovableMedia: false),
            new RemovableDrive(2, "SanDisk Ultra",     61_530_439_680L, "USB",  IsSystemDisk: false, HasRemovableMedia: true),
        });

        var targets = BuildWriter().GetCandidateTargets(includeUsbHardDrives: false);

        targets.Should().ContainSingle().Which.DiskNumber.Should().Be(2);
    }

    [Fact]
    public void GetCandidateTargets_SystemDiskOnUsb_IsRefused()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Odd controller", 500_000_000_000L, "USB", IsSystemDisk: true, HasRemovableMedia: true),
        });

        var targets = BuildWriter().GetCandidateTargets(includeUsbHardDrives: false);

        targets.Should().BeEmpty();
    }

    [Fact]
    public void GetCandidateTargets_NonUsbDataDisk_IsNotOffered()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(1, "Seagate Barracuda", 4_000_787_030_016L, "SATA", IsSystemDisk: false, HasRemovableMedia: false),
        });

        var targets = BuildWriter().GetCandidateTargets(includeUsbHardDrives: false);

        targets.Should().BeEmpty();
    }

    [Fact]
    public void GetCandidateTargets_UsbHardDrive_IsNotOffered()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(3, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false),
            new RemovableDrive(2, "SanDisk Ultra",  61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true),
        });

        var targets = BuildWriter().GetCandidateTargets(includeUsbHardDrives: false);

        targets.Should().ContainSingle().Which.DiskNumber.Should().Be(2);
    }

    [Fact]
    public void GetCandidateTargets_UsbHardDriveWithOptIn_IsOffered()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(3, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false),
            new RemovableDrive(2, "SanDisk Ultra",  61_530_439_680L, "USB", IsSystemDisk: false, HasRemovableMedia: true),
        });

        var targets = BuildWriter().GetCandidateTargets(includeUsbHardDrives: true);

        targets.Select(t => t.DiskNumber).Should().Equal(2, 3);
    }

    [Fact]
    public void GetCandidateTargets_WithOptIn_StillRefusesSystemAndNonUsbDisks()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Odd controller", 500_000_000_000L, "USB", IsSystemDisk: true, HasRemovableMedia: false),
            new RemovableDrive(1, "Seagate Barracuda", 4_000_787_030_016L, "SATA", IsSystemDisk: false, HasRemovableMedia: false),
        });

        var targets = BuildWriter().GetCandidateTargets(includeUsbHardDrives: true);

        targets.Should().BeEmpty();
    }

    [Fact]
    public void Write_UsbHardDriveTheUserOptedInTo_IsNotRefusedAsAnUnknownTarget()
    {
        var hardDrive = new RemovableDrive(3, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false);
        GivenStick(hardDrive, (@"sources\install.wim", 3_000_000_000L));
        _operations.Setup(o => o.GetDriveLetters(3)).Returns([]);
        _operations.Setup(o => o.CreateBootPartition(3, It.IsAny<long>())).Returns(1);
        _operations.Setup(o => o.CreateInstallPartition(3)).Returns(2);
        _operations.Setup(o => o.AssignDriveLetter(3, 1)).Returns('E');
        _operations.Setup(o => o.AssignDriveLetter(3, 2)).Returns('F');

        BuildWriter().Write(hardDrive, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        _operations.Verify(o => o.Clear(3), Times.Once);
    }

    [Fact]
    public void Write_DiskIsNotACandidate_ErasesNothing()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[]
        {
            new RemovableDrive(0, "Samsung SSD 990", 2_000_398_934_016L, "NVMe", IsSystemDisk: true, HasRemovableMedia: false),
        });
        var systemDisk = new RemovableDrive(0, "Samsung SSD 990", 2_000_398_934_016L, "NVMe", IsSystemDisk: true, HasRemovableMedia: false);

        Action act = () => BuildWriter().Write(systemDisk, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>();
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_AnotherDriveHoldsThePickedDisksNumber_ErasesNothing()
    {
        GivenStick(new RemovableDrive(2, "WD Elements", 2_000_398_934_016L, "USB", IsSystemDisk: false, HasRemovableMedia: false));

        Action act = () => BuildWriter().Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>();
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_DriveTooSmallForBootPartitionPlusPayload_RefusesBeforeErasing()
    {
        var stick = new RemovableDrive(2, "Small stick", 7_000_000_000L, "USB", IsSystemDisk: false, HasRemovableMedia: true);
        GivenStick(stick, (@"sources\install.wim", 5_500_000_000L));

        Action act = () => BuildWriter().Write(stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>();
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_BootFilesOutgrowTheBootPartition_RefusesBeforeErasing()
    {
        GivenStick(Stick, (@"sources\boot.wim", 2_500_000_000L));

        Action act = () => BuildWriter().Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*boot.wim*");
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_ValidTarget_PartitionsAndFormatsBothBeforeCopying()
    {
        GivenStick(Stick, (@"sources\install.wim", 7_578_075_168L));
        GivenPartitions();
        var order = new List<string>();
        _operations.Setup(o => o.Clear(2)).Callback(() => order.Add("Clear"));
        _operations.Setup(o => o.EnsureMbr(2)).Callback(() => order.Add("EnsureMbr"));
        _operations.Setup(o => o.CreateBootPartition(2, UsbWriteLayoutPlanner.BootPartitionBytes)).Returns(1).Callback(() => order.Add("CreateBoot"));
        _operations.Setup(o => o.CreateInstallPartition(2)).Returns(2).Callback(() => order.Add("CreateInstall"));
        _operations.Setup(o => o.Format(2, 1, "FAT32", "WINBOOT")).Callback(() => order.Add("FormatBoot"));
        _operations.Setup(o => o.Format(2, 2, "NTFS", "WINSETUP")).Callback(() => order.Add("FormatInstall"));
        var copier = new Mock<IMediaCopier>();
        copier.Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(), It.IsAny<long?>(),
                  It.IsAny<long>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
              .Callback(() => order.Add("Copy"));

        BuildWriter(copier.Object).Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        order.Should().Equal("Clear", "EnsureMbr", "CreateBoot", "CreateInstall", "FormatBoot", "FormatInstall", "Copy", "Copy");
    }

    [Fact]
    public void Write_ImageOver4Gb_IsCopiedWholeToTheInstallPartitionOnOneBar()
    {
        GivenStick(Stick, (@"sources\boot.wim", 600_000_000L), (@"sources\install.wim", 7_578_075_168L));
        GivenPartitions();
        var copies = new List<(string Root, Func<string, bool> Skip, long? Total, long Before)>();
        var copier = new Mock<IMediaCopier>();
        copier.Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(), It.IsAny<long?>(),
                  It.IsAny<long>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
              .Callback<string, string, Func<string, bool>?, long?, long, IProgress<TaskProgressDetail>?, CancellationToken>(
                  (_, root, skip, total, before, _, _) => copies.Add((root, skip!, total, before)));

        BuildWriter(copier.Object).Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        copies.Should().HaveCount(2);
        var (bootRoot, bootSkip, bootTotal, bootBefore) = copies[0];
        bootRoot.Should().Be(@"E:\");
        bootBefore.Should().Be(0);
        bootSkip(@"C:\work\sources\boot.wim").Should().BeFalse();
        bootSkip(@"C:\work\sources\install.wim").Should().BeTrue();
        bootSkip(@"C:\work\support").Should().BeTrue();

        var (installRoot, installSkip, installTotal, installBefore) = copies[1];
        installRoot.Should().Be(@"F:\");
        installSkip(@"C:\work\sources\install.wim").Should().BeFalse();
        installSkip(@"C:\work\efi\boot").Should().BeTrue();
        installBefore.Should().Be(600_000_000L, "the second pass carries on the first pass's bar");
        bootTotal.Should().Be(600_000_000L + 600_000_000L + 7_578_075_168L);
        installTotal.Should().Be(bootTotal);
    }

    [Fact]
    public void Write_Replacements_LandOnBothPartitionsButNeverTheInstallFirmwarePath()
    {
        GivenStick(Stick, (@"sources\install.wim", 3_000_000_000L));
        GivenPartitions();
        var replacements = new BootFileReplacements(new Dictionary<string, string>
        {
            [@"efi\boot\bootx64.efi"] = @"C:\temp\bootmgfw_EX.efi",
            ["bootmgr.efi"] = @"C:\temp\bootmgr_EX.efi",
        });

        BuildWriter().Write(Stick, @"C:\work", replacements, null, CancellationToken.None);

        _fileSystem.Verify(fs => fs.CopyFile(@"C:\temp\bootmgfw_EX.efi", @"E:\efi\boot\bootx64.efi", true), Times.Once);
        _fileSystem.Verify(fs => fs.CopyFile(@"C:\temp\bootmgfw_EX.efi", @"F:\efi\boot\bootx64.efi", It.IsAny<bool>()), Times.Never);
        _fileSystem.Verify(fs => fs.CopyFile(@"C:\temp\bootmgr_EX.efi", @"E:\bootmgr.efi", true), Times.Once);
        _fileSystem.Verify(fs => fs.CopyFile(@"C:\temp\bootmgr_EX.efi", @"F:\bootmgr.efi", true), Times.Once);
    }

    [Fact]
    public void Write_TargetHoldsTheWorkingFolder_RefusesBeforeErasing()
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[] { Stick });
        _operations.Setup(o => o.GetDriveLetters(2)).Returns(['F']);
        _fileSystem.Setup(fs => fs.GetPathRoot(@"F:\work")).Returns(@"F:\");

        Action act = () => BuildWriter().Write(Stick, @"F:\work", BootFileReplacements.None, null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SanDisk Ultra*");
        _operations.Verify(o => o.Clear(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Write_StepFailsAfterTheErase_ReportsTheDriveAsErased()
    {
        GivenStick(Stick, (@"sources\install.wim", 3_000_000_000L));
        _operations.Setup(o => o.Format(2, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                   .Throws(new InvalidOperationException("Could not format partition 1 on disk 2: The device is not ready."));

        Action act = () => BuildWriter().Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        var erased = act.Should().Throw<UsbMediaErasedException>().Which;
        erased.Target.Should().Be(Stick);
        erased.WasCancelled.Should().BeFalse();
        erased.Message.Should().Contain("not ready");
        _operations.Verify(o => o.Clear(2), Times.Once);
    }

    [Fact]
    public void Write_EraseItselfFails_ReportsTheDriveAsErased()
    {
        GivenStick(Stick, (@"sources\install.wim", 3_000_000_000L));
        _operations.Setup(o => o.Clear(2))
                   .Throws(new InvalidOperationException("Disk 2 still reports no free space after the wipe."));

        Action act = () => BuildWriter().Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        var erased = act.Should().Throw<UsbMediaErasedException>().Which;
        erased.Target.Should().Be(Stick);
        erased.WasCancelled.Should().BeFalse();
        erased.Message.Should().Contain("no free space");
    }

    [Fact]
    public void Write_CancelledDuringTheCopy_ReportsTheDriveAsErased()
    {
        GivenStick(Stick, (@"sources\install.wim", 3_000_000_000L));
        GivenPartitions();
        var copier = new Mock<IMediaCopier>();
        copier.Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(), It.IsAny<long?>(),
                  It.IsAny<long>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
              .Throws(new OperationCanceledException());

        Action act = () => BuildWriter(copier.Object).Write(Stick, @"C:\work", BootFileReplacements.None, null, CancellationToken.None);

        act.Should().Throw<UsbMediaErasedException>().Which.WasCancelled.Should().BeTrue();
    }

    private void GivenStick(RemovableDrive stick, params (string RelativePath, long Size)[] files)
    {
        _operations.Setup(o => o.GetDisks()).Returns(new[] { stick });
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work", "*", SearchOption.AllDirectories))
                   .Returns(files.Select(f => @"C:\work\" + f.RelativePath).ToArray());
        foreach (var (relativePath, size) in files)
        {
            _fileSystem.Setup(fs => fs.GetFileSize(@"C:\work\" + relativePath)).Returns(size);
        }
    }

    private void GivenPartitions()
    {
        _operations.Setup(o => o.CreateBootPartition(2, It.IsAny<long>())).Returns(1);
        _operations.Setup(o => o.CreateInstallPartition(2)).Returns(2);
        _operations.Setup(o => o.AssignDriveLetter(2, 1)).Returns('E');
        _operations.Setup(o => o.AssignDriveLetter(2, 2)).Returns('F');
    }

    private StorageApiUsbMediaWriter BuildWriter(IMediaCopier? mediaCopier = null)
    {
        return new StorageApiUsbMediaWriter(
            _operations.Object,
            mediaCopier ?? Mock.Of<IMediaCopier>(),
            _fileSystem.Object,
            Mock.Of<ILocalizationService>(),
            Mock.Of<ILogService>());
    }
}
