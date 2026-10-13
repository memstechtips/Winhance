using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class StagedAppInstallerTests
{
    private const string AppsFolder = @"C:\ProgramData\Winhance\Unattend\Apps";
    private const string CustomFolder = @"C:\ProgramData\Winhance\Unattend\Apps\Custom";

    private const string WixManifest = """
        Installers:
        - InstallerType: wix
          InstallerSwitches:
            Silent: /quiet /norestart
        ManifestType: merged
        """;

    private const string VlcAppFolder = AppsFolder + @"\external-app-vlc";
    private static readonly string VlcFolder = $@"{VlcAppFolder}\{ArchitectureHelper.GetCurrentArchitecture()}";
    private static readonly string VlcMsi = VlcFolder + @"\vlc.msi";
    private static readonly StagedInstaller StagedVlc = new("VLC", VlcFolder);

    private static readonly ItemDefinition Vlc = new()
    {
        Id = "external-app-vlc",
        Name = "VLC",
        Description = "Media player"
    };

    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();
    private readonly Mock<IDirectDownloadService> _directDownloads = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<ILocalizationService> _localization = new();

    public StagedAppInstallerTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _files.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _files.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
    }

    private StagedAppInstaller CreateSut() => new(
        _files.Object,
        _registry.Object,
        _processExecutor.Object,
        _directDownloads.Object,
        _log.Object,
        _localization.Object);

    private void Stage(string folder, params string[] fileNames)
    {
        _files.Setup(f => f.DirectoryExists(folder)).Returns(true);
        _files.Setup(f => f.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
            .Returns(fileNames.Select(name => folder + "\\" + name).ToArray());
    }

    private void StageSubfolders(string folder, params string[] names)
    {
        _files.Setup(f => f.DirectoryExists(folder)).Returns(true);
        _files.Setup(f => f.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly))
            .Returns(names.Select(name => folder + "\\" + name).ToArray());
    }

    private void StageVlcWithManifest(string manifest)
    {
        Stage(VlcFolder, "vlc.msi", "vlc.yaml");
        _files.Setup(f => f.ReadAllTextAsync(VlcFolder + @"\vlc.yaml", It.IsAny<CancellationToken>())).ReturnsAsync(manifest);
    }

    private void ArrangeExitCode(int exitCode) => _processExecutor
        .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ProcessExecutionResult { ExitCode = exitCode });

    private void VerifyFolderDeleted(Times times) =>
        _files.Verify(f => f.DeleteDirectory(VlcAppFolder, true), times);

    [Fact]
    public void Find_FolderMissing_ReturnsNull()
    {
        CreateSut().Find(Vlc).Should().BeNull();
    }

    [Fact]
    public void Find_StagedForThisArchitecture_ReturnsThatFolder()
    {
        Stage(VlcFolder, "vlc.msi", "vlc.yaml");

        CreateSut().Find(Vlc).Should().Be(StagedVlc);
    }

    [Fact]
    public void Find_StagedOnlyForAnotherArchitecture_ReturnsNull()
    {
        var otherArchitecture = ArchitectureHelper.GetCurrentArchitecture() == "arm64" ? "x64" : "arm64";
        Stage($@"{AppsFolder}\external-app-vlc\{otherArchitecture}", "vlc.msi");

        CreateSut().Find(Vlc).Should().BeNull();
    }

    [Fact]
    public async Task InstallAsync_WixManifest_RunsMsiexecWithItsSwitchesAndDeletesTheFolder()
    {
        StageVlcWithManifest(WixManifest);
        ArrangeExitCode(0);

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeTrue();
        _processExecutor.Verify(
            p => p.ExecuteAsync("msiexec.exe", $"/i \"{VlcMsi}\" /quiet /norestart", It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyFolderDeleted(Times.Once());
    }

    [Theory]
    [InlineData(3010)]
    [InlineData(1641)]
    public async Task InstallAsync_RestartExitCode_Succeeds(int exitCode)
    {
        StageVlcWithManifest(WixManifest);
        ArrangeExitCode(exitCode);

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeTrue();
        VerifyFolderDeleted(Times.Once());
    }

    [Fact]
    public async Task InstallAsync_AnswerFileSaysKeep_LeavesTheFolder()
    {
        StageVlcWithManifest(WixManifest);
        ArrangeExitCode(0);
        _registry.Setup(r => r.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance", "KeepStagedInstallers")).Returns(1);

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeTrue();
        VerifyFolderDeleted(Times.Never());
    }

    [Fact]
    public async Task InstallAsync_InstallerStillRunning_DeletesTheFolderOnALaterTry()
    {
        StageVlcWithManifest(WixManifest);
        ArrangeExitCode(0);
        _files.SetupSequence(f => f.DeleteDirectory(VlcAppFolder, true))
            .Throws(new UnauthorizedAccessException("Access to the path 'vlc.msi' is denied."))
            .Pass();

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeTrue();
        VerifyFolderDeleted(Times.Exactly(2));
    }

    [Fact]
    public async Task InstallAsync_FailingExitCode_FailsAndKeepsTheFolder()
    {
        StageVlcWithManifest(WixManifest);
        ArrangeExitCode(1603);

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeFalse();
        _files.Verify(f => f.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task InstallAsync_NoManifest_InstallsByFileType()
    {
        Stage(VlcFolder, "vlc.msi");
        _directDownloads
            .Setup(d => d.InstallFileAsync(VlcMsi, "VLC", It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeTrue();
        _processExecutor.Verify(
            p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyFolderDeleted(Times.Once());
    }

    [Fact]
    public async Task InstallAsync_OwnInstaller_DeletesOnlyItsOwnFolder()
    {
        var toolFolder = CustomFolder + @"\Tool";
        Stage(toolFolder, "tool.exe");
        _directDownloads
            .Setup(d => d.InstallFileAsync(toolFolder + @"\tool.exe", "Tool", It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var installed = await CreateSut().InstallAsync(new StagedInstaller("Tool", toolFolder));

        installed.Should().BeTrue();
        _files.Verify(f => f.DeleteDirectory(toolFolder, true), Times.Once);
        _files.Verify(f => f.DeleteDirectory(CustomFolder, It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task InstallAsync_ManifestWithoutASilentSwitch_InstallsByFileType()
    {
        StageVlcWithManifest("""
            Installers:
            - InstallerType: exe
            """);
        _directDownloads
            .Setup(d => d.InstallFileAsync(VlcMsi, "VLC", It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var installed = await CreateSut().InstallAsync(StagedVlc);

        installed.Should().BeFalse();
        _directDownloads.Verify(
            d => d.InstallFileAsync(VlcMsi, "VLC", It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyFolderDeleted(Times.Never());
    }

    [Fact]
    public void FindAll_FolderMissing_ReturnsEmpty()
    {
        CreateSut().FindAll(CustomFolder).Should().BeEmpty();
    }

    [Fact]
    public void FindAll_ListsOnlySubfoldersHoldingOneInstaller()
    {
        StageSubfolders(CustomFolder, "Acrobat", "Empty", "OnlyManifest", "TwoInstallers");
        Stage(CustomFolder + @"\Acrobat", "AcroRdr.exe", "AcroRdr.yaml");
        Stage(CustomFolder + @"\Empty");
        Stage(CustomFolder + @"\OnlyManifest", "setup.yaml");
        Stage(CustomFolder + @"\TwoInstallers", "a.exe", "b.msi");

        CreateSut().FindAll(CustomFolder).Should().Equal(new StagedInstaller("Acrobat", CustomFolder + @"\Acrobat"));
    }
}
