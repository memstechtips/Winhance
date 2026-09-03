using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class DriverCategorizerTests
{
    private static readonly string[] HeciSubfolders = ["C:\\Source\\Heci\\x64"];
    private static readonly string[] HeciPayload = ["C:\\Source\\Heci\\x64\\heci.sys"];
    private static readonly string[] GfxSubfolders = ["C:\\Source\\Gfx\\ext"];

    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly DriverCategorizer _sut;

    public DriverCategorizerTests()
    {
        _fileSystemService
            .Setup(f => f.GetDirectories(It.IsAny<string>()))
            .Returns(Array.Empty<string>());
        _sut = new DriverCategorizer(_logService.Object, _fileSystemService.Object);
    }

    [Theory]
    [InlineData("iaahci.inf")]
    [InlineData("iastor.inf")]
    [InlineData("iastorac.inf")]
    [InlineData("iastora.inf")]
    [InlineData("iastorv.inf")]
    [InlineData("vmd.inf")]
    [InlineData("irst.inf")]
    [InlineData("rst.inf")]
    public void IsStorageDriver_StorageFilenameKeyword_ReturnsTrue(string fileName)
    {
        var infPath = $"C:\\Drivers\\{fileName}";
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns(fileName);

        var result = _sut.IsStorageDriver(infPath);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsStorageDriver_NonStorageFilename_ChecksFileContent()
    {
        var infPath = "C:\\Drivers\\network.inf";
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("network.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Returns("[Version]\nClass=Net\nClassGuid={something}");

        var result = _sut.IsStorageDriver(infPath);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("SCSIAdapter")]
    [InlineData("hdc")]
    [InlineData("HDC")]
    public void IsStorageDriver_StorageClass_ReturnsTrue(string className)
    {
        var infPath = "C:\\Drivers\\storage.inf";
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("storage.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Returns($"[Version]\nClass = {className}\nClassGuid={{something}}");

        var result = _sut.IsStorageDriver(infPath);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsStorageDriver_NonStorageClass_ReturnsFalse()
    {
        var infPath = "C:\\Drivers\\display.inf";
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("display.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Returns("[Version]\nClass=Display\nClassGuid={something}");

        var result = _sut.IsStorageDriver(infPath);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsStorageDriver_UnicodeReadFails_FallsBackToUtf8()
    {
        var infPath = "C:\\Drivers\\storage.inf";
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("storage.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Throws(new IOException("Cannot read"));
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.UTF8))
            .Returns("[Version]\nClass=SCSIAdapter\nClassGuid={something}");

        var result = _sut.IsStorageDriver(infPath);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsStorageDriver_ExceptionThrown_ReturnsFalse()
    {
        var infPath = "C:\\Drivers\\bad.inf";
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("bad.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Throws(new IOException("Disk error"));
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.UTF8))
            .Throws(new IOException("Disk error"));

        var result = _sut.IsStorageDriver(infPath);

        result.Should().BeFalse();
        _logService.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Could not categorize driver"))), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_NoInfFiles_ReturnsZero()
    {
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(0);
        _logService.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("No .inf files"))), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_StorageDriver_CopiesToWinPePath()
    {
        var infPath = "C:\\Source\\DriverFolder\\iastor.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("iastor.inf");
        _fileSystemService.Setup(f => f.GetDirectoryName(infPath)).Returns("C:\\Source\\DriverFolder");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\DriverFolder")).Returns("DriverFolder");
        _fileSystemService.Setup(f => f.CombinePath("C:\\WinPE", "DriverFolder")).Returns("C:\\WinPE\\DriverFolder");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\WinPE\\DriverFolder")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\DriverFolder"))
            .Returns(new[] { infPath, "C:\\Source\\DriverFolder\\iastor.sys" });
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\DriverFolder\\iastor.sys")).Returns("iastor.sys");
        _fileSystemService.Setup(f => f.CombinePath("C:\\WinPE\\DriverFolder", "iastor.inf")).Returns("C:\\WinPE\\DriverFolder\\iastor.inf");
        _fileSystemService.Setup(f => f.CombinePath("C:\\WinPE\\DriverFolder", "iastor.sys")).Returns("C:\\WinPE\\DriverFolder\\iastor.sys");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\WinPE\\DriverFolder"), Times.Once);
        _fileSystemService.Verify(f => f.CopyFile(infPath, "C:\\WinPE\\DriverFolder\\iastor.inf", true), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_NonStorageDriver_CopiesToOemPath()
    {
        var infPath = "C:\\Source\\NetDriver\\network.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("network.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Returns("[Version]\nClass=Net");
        _fileSystemService.Setup(f => f.GetDirectoryName(infPath)).Returns("C:\\Source\\NetDriver");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\NetDriver")).Returns("NetDriver");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "NetDriver")).Returns("C:\\OEM\\NetDriver");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\NetDriver")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\NetDriver"))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\NetDriver", "network.inf")).Returns("C:\\OEM\\NetDriver\\network.inf");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\OEM\\NetDriver"), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_WithWorkingDirectoryExclude_FiltersDrivers()
    {
        var excludedInf = "C:\\Work\\Temp\\driver.inf";
        var validInf = "C:\\Source\\OtherDriver\\network.inf";

        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { excludedInf, validInf });
        _fileSystemService.Setup(f => f.GetFileName(validInf)).Returns("network.inf");
        _fileSystemService.Setup(f => f.ReadAllText(validInf, Encoding.Unicode))
            .Returns("[Version]\nClass=Net");
        _fileSystemService.Setup(f => f.GetDirectoryName(validInf)).Returns("C:\\Source\\OtherDriver");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\OtherDriver")).Returns("OtherDriver");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "OtherDriver")).Returns("C:\\OEM\\OtherDriver");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\OtherDriver")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\OtherDriver"))
            .Returns(new[] { validInf });
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\OtherDriver", "network.inf")).Returns("C:\\OEM\\OtherDriver\\network.inf");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM", "C:\\Work");

        result.Should().Be(1);
        _logService.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("Excluded 1 driver"))), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_DuplicateTargetDir_AppendsSuffix()
    {
        var infPath = "C:\\Source\\DriverFolder\\network.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("network.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Returns("[Version]\nClass=Net");
        _fileSystemService.Setup(f => f.GetDirectoryName(infPath)).Returns("C:\\Source\\DriverFolder");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\DriverFolder")).Returns("DriverFolder");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "DriverFolder")).Returns("C:\\OEM\\DriverFolder");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "DriverFolder_1")).Returns("C:\\OEM\\DriverFolder_1");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\DriverFolder")).Returns(true);
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\DriverFolder_1")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\DriverFolder"))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\DriverFolder_1", "network.inf")).Returns("C:\\OEM\\DriverFolder_1\\network.inf");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\OEM\\DriverFolder_1"), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_MultipleInfsInSameFolder_ProcessedOnce()
    {
        var inf1 = "C:\\Source\\DriverFolder\\driver1.inf";
        var inf2 = "C:\\Source\\DriverFolder\\driver2.inf";

        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { inf1, inf2 });
        _fileSystemService.Setup(f => f.GetFileName(inf1)).Returns("driver1.inf");
        _fileSystemService.Setup(f => f.GetFileName(inf2)).Returns("driver2.inf");
        _fileSystemService.Setup(f => f.ReadAllText(inf1, Encoding.Unicode))
            .Returns("[Version]\nClass=Net");
        _fileSystemService.Setup(f => f.GetDirectoryName(inf1)).Returns("C:\\Source\\DriverFolder");
        _fileSystemService.Setup(f => f.GetDirectoryName(inf2)).Returns("C:\\Source\\DriverFolder");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\DriverFolder")).Returns("DriverFolder");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "DriverFolder")).Returns("C:\\OEM\\DriverFolder");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\DriverFolder")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\DriverFolder"))
            .Returns(new[] { inf1, inf2 });
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\DriverFolder", "driver1.inf")).Returns("C:\\OEM\\DriverFolder\\driver1.inf");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\DriverFolder", "driver2.inf")).Returns("C:\\OEM\\DriverFolder\\driver2.inf");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\OEM\\DriverFolder"), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_PayloadInASubfolder_CopiesTheWholeTree()
    {
        var infPath = "C:\\Source\\Heci\\heci.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("heci.inf");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode)).Returns("[Version]\nClass=System");
        _fileSystemService.Setup(f => f.GetDirectoryName(infPath)).Returns("C:\\Source\\Heci");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\Heci")).Returns("Heci");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "Heci")).Returns("C:\\OEM\\Heci");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\Heci")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\Heci")).Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.GetDirectories("C:\\Source\\Heci")).Returns(HeciSubfolders);
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\Heci\\x64")).Returns("x64");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\Heci", "heci.inf")).Returns("C:\\OEM\\Heci\\heci.inf");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\Heci", "x64")).Returns("C:\\OEM\\Heci\\x64");
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\Heci\\x64")).Returns(HeciPayload);
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\Heci\\x64\\heci.sys")).Returns("heci.sys");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\Heci\\x64", "heci.sys")).Returns("C:\\OEM\\Heci\\x64\\heci.sys");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\OEM\\Heci\\x64"), Times.Once);
        _fileSystemService.Verify(f => f.CopyFile("C:\\Source\\Heci\\x64\\heci.sys", "C:\\OEM\\Heci\\x64\\heci.sys", true), Times.Once);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_InfInsideAPackage_IsNotItsOwnPackage()
    {
        var rootInf = "C:\\Source\\Gfx\\iigd_dch.inf";
        var nestedInf = "C:\\Source\\Gfx\\ext\\iigd_ext.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { rootInf, nestedInf });
        _fileSystemService.Setup(f => f.GetFileName(rootInf)).Returns("iigd_dch.inf");
        _fileSystemService.Setup(f => f.GetFileName(nestedInf)).Returns("iigd_ext.inf");
        _fileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>(), It.IsAny<Encoding>())).Returns("[Version]\nClass=Display");
        _fileSystemService.Setup(f => f.GetDirectoryName(rootInf)).Returns("C:\\Source\\Gfx");
        _fileSystemService.Setup(f => f.GetDirectoryName(nestedInf)).Returns("C:\\Source\\Gfx\\ext");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\Gfx")).Returns("Gfx");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "Gfx")).Returns("C:\\OEM\\Gfx");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\Gfx")).Returns(false);
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\Gfx")).Returns(new[] { rootInf });
        _fileSystemService.Setup(f => f.GetDirectories("C:\\Source\\Gfx")).Returns(GfxSubfolders);
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\Gfx\\ext")).Returns("ext");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\Gfx", "iigd_dch.inf")).Returns("C:\\OEM\\Gfx\\iigd_dch.inf");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\Gfx", "ext")).Returns("C:\\OEM\\Gfx\\ext");
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source\\Gfx\\ext")).Returns(new[] { nestedInf });
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM\\Gfx\\ext", "iigd_ext.inf")).Returns("C:\\OEM\\Gfx\\ext\\iigd_ext.inf");

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\OEM\\Gfx"), Times.Once);
        _fileSystemService.Verify(f => f.CopyFile(nestedInf, "C:\\OEM\\Gfx\\ext\\iigd_ext.inf", true), Times.Once);
        _fileSystemService.Verify(f => f.CombinePath("C:\\OEM", "ext"), Times.Never);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_CopyFailure_LogsErrorAndContinues()
    {
        var infPath = "C:\\Source\\DriverFolder\\network.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { infPath });
        _fileSystemService.Setup(f => f.GetFileName(infPath)).Returns("network.inf");
        _fileSystemService.Setup(f => f.GetDirectoryName(infPath)).Returns("C:\\Source\\DriverFolder");
        _fileSystemService.Setup(f => f.ReadAllText(infPath, Encoding.Unicode))
            .Returns("[Version]\nClass=Net");
        _fileSystemService.Setup(f => f.GetFileName("C:\\Source\\DriverFolder")).Returns("DriverFolder");
        _fileSystemService.Setup(f => f.CombinePath("C:\\OEM", "DriverFolder")).Returns("C:\\OEM\\DriverFolder");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\OEM\\DriverFolder")).Returns(false);
        _fileSystemService.Setup(f => f.CreateDirectory("C:\\OEM\\DriverFolder"))
            .Throws(new IOException("Permission denied"));

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM");

        result.Should().Be(0);
        _logService.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Failed to copy driver")),
            It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public void MoveStorageDrivers_MovesOnlyStoragePackagesAndCountsAll()
    {
        var storageInf = "C:\\OEM\\RstPkg\\iastor.inf";
        var netInf = "C:\\OEM\\NetPkg\\network.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\OEM", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { storageInf, netInf });
        _fileSystemService.Setup(f => f.GetFileName(storageInf)).Returns("iastor.inf");
        _fileSystemService.Setup(f => f.GetFileName(netInf)).Returns("network.inf");
        _fileSystemService.Setup(f => f.ReadAllText(netInf, Encoding.Unicode)).Returns("[Version]\nClass=Net");
        _fileSystemService.Setup(f => f.GetDirectoryName(storageInf)).Returns("C:\\OEM\\RstPkg");
        _fileSystemService.Setup(f => f.GetDirectoryName(netInf)).Returns("C:\\OEM\\NetPkg");
        _fileSystemService.Setup(f => f.GetFileName("C:\\OEM\\RstPkg")).Returns("RstPkg");
        _fileSystemService.Setup(f => f.CombinePath("C:\\WinPE", "RstPkg")).Returns("C:\\WinPE\\RstPkg");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\WinPE\\RstPkg")).Returns(false);

        var result = _sut.MoveStorageDrivers("C:\\OEM", "C:\\WinPE");

        result.Should().Be(2);
        _fileSystemService.Verify(f => f.CreateDirectory("C:\\WinPE"), Times.Once);
        _fileSystemService.Verify(f => f.MoveDirectory("C:\\OEM\\RstPkg", "C:\\WinPE\\RstPkg"), Times.Once);
        _fileSystemService.Verify(f => f.MoveDirectory("C:\\OEM\\NetPkg", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void MoveStorageDrivers_OnePackageFailsToMove_LogsItAndMovesTheRest()
    {
        var failingInf = "C:\\OEM\\RstPkg\\iastor.inf";
        var movedInf = "C:\\OEM\\VmdPkg\\vmd.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\OEM", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { failingInf, movedInf });
        _fileSystemService.Setup(f => f.GetFileName(failingInf)).Returns("iastor.inf");
        _fileSystemService.Setup(f => f.GetFileName(movedInf)).Returns("vmd.inf");
        _fileSystemService.Setup(f => f.GetDirectoryName(failingInf)).Returns("C:\\OEM\\RstPkg");
        _fileSystemService.Setup(f => f.GetDirectoryName(movedInf)).Returns("C:\\OEM\\VmdPkg");
        _fileSystemService.Setup(f => f.GetFileName("C:\\OEM\\RstPkg")).Returns("RstPkg");
        _fileSystemService.Setup(f => f.GetFileName("C:\\OEM\\VmdPkg")).Returns("VmdPkg");
        _fileSystemService.Setup(f => f.CombinePath("C:\\WinPE", "RstPkg")).Returns("C:\\WinPE\\RstPkg");
        _fileSystemService.Setup(f => f.CombinePath("C:\\WinPE", "VmdPkg")).Returns("C:\\WinPE\\VmdPkg");
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\WinPE\\RstPkg")).Returns(false);
        _fileSystemService.Setup(f => f.DirectoryExists("C:\\WinPE\\VmdPkg")).Returns(false);
        _fileSystemService.Setup(f => f.MoveDirectory("C:\\OEM\\RstPkg", "C:\\WinPE\\RstPkg"))
            .Throws(new IOException("Permission denied"));

        var result = _sut.MoveStorageDrivers("C:\\OEM", "C:\\WinPE");

        result.Should().Be(2);
        _logService.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Failed to move storage driver package RstPkg")),
            It.IsAny<Exception>()), Times.Once);
        _fileSystemService.Verify(f => f.MoveDirectory("C:\\OEM\\VmdPkg", "C:\\WinPE\\VmdPkg"), Times.Once);
    }

    [Fact]
    public void MoveStorageDrivers_LooseInfAtTheStagingRoot_NeverMovesTheRoot()
    {
        var looseInf = "C:\\OEM\\iastor.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\OEM", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { looseInf });
        _fileSystemService.Setup(f => f.GetFileName(looseInf)).Returns("iastor.inf");
        _fileSystemService.Setup(f => f.GetDirectoryName(looseInf)).Returns("C:\\OEM");

        var result = _sut.MoveStorageDrivers("C:\\OEM", "C:\\WinPE");

        result.Should().Be(1);
        _fileSystemService.Verify(f => f.MoveDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CategorizeAndCopyDrivers_AllFilesExcluded_ReturnsZero()
    {
        var excludedInf = "C:\\Work\\driver.inf";
        _fileSystemService.Setup(f => f.GetFiles("C:\\Source", "*.inf", SearchOption.AllDirectories))
            .Returns(new[] { excludedInf });

        var result = _sut.CategorizeAndCopyDrivers("C:\\Source", "C:\\WinPE", "C:\\OEM", "C:\\Work");

        result.Should().Be(0);
        _logService.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("No valid drivers"))), Times.Once);
    }
}
