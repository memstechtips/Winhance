using FluentAssertions;
using Microsoft.Win32;
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
    private static readonly StagedInstaller StagedVlc = new("VLC", VlcFolder, "external-app-vlc");

    private static readonly string ToolFolder = $@"{AppsFolder}\external-app-tool\{ArchitectureHelper.GetCurrentArchitecture()}";
    private static readonly StagedInstaller StagedTool = new("Tool", ToolFolder);
    private static readonly StagedInstaller StagedCatalogTool = new("Tool", ToolFolder, "external-app-tool");
    private const string UserLocalAppData = @"C:\Users\Test\AppData\Local";
    private const string UserPrograms = @"C:\Users\Test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs";
    private static readonly string UserToolRoot = $@"{UserLocalAppData}\Microsoft\WinGet\Packages\Example.Tool_Microsoft.Winget.Source_8wekyb3d8bbwe";
    private static readonly string UserLinks = $@"{UserLocalAppData}\Microsoft\WinGet\Links";
    private static readonly string CommonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
    private const string UserEnvironmentKey = @"HKEY_CURRENT_USER\Environment";
    private const string UserToolUninstallKey = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Example.Tool_Microsoft.Winget.Source_8wekyb3d8bbwe";
    private static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    private static readonly string ToolRoot = $@"{ProgramFiles}\WinGet\Packages\Example.Tool_Microsoft.Winget.Source_8wekyb3d8bbwe";
    private static readonly string Links = $@"{ProgramFiles}\WinGet\Links";
    private const string EnvironmentKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string ToolUninstallKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Example.Tool_Microsoft.Winget.Source_8wekyb3d8bbwe";

    private const string PortableZipManifest = """
        PackageIdentifier: Example.Tool
        PackageName: Tool
        PackageVersion: 2.1.0
        Publisher: Example
        Installers:
        - InstallerType: zip
          NestedInstallerType: portable
          NestedInstallerFiles:
          - RelativeFilePath: tool\tool.exe
            PortableCommandAlias: tool
        ManifestType: merged
        """;

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
    private readonly Mock<IWindowsUIManagementService> _windowsUI = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IInstallOptionsService> _installOptions = new();
    private readonly Mock<IInteractiveUserService> _interactiveUser = new();

    public StagedAppInstallerTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _files.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string path) => Path.GetFileName(path));
        _files.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
        _files.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>())).Returns((string path) => Path.GetFileNameWithoutExtension(path));
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default);
        _installOptions.Setup(o => o.EffectiveFor(It.IsAny<string>())).Returns(InstallOptions.Default);
        _interactiveUser.Setup(u => u.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)).Returns(UserLocalAppData);
        _interactiveUser.Setup(u => u.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs)).Returns(UserPrograms);
    }

    private StagedAppInstaller CreateSut() => new(
        _files.Object,
        _registry.Object,
        _processExecutor.Object,
        _directDownloads.Object,
        _windowsUI.Object,
        _log.Object,
        _localization.Object,
        _installOptions.Object,
        _interactiveUser.Object);

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

    private void StageTool(string installer, string manifest)
    {
        Stage(ToolFolder, installer, "tool.yaml");
        _files.Setup(f => f.ReadAllTextAsync(ToolFolder + @"\tool.yaml", It.IsAny<CancellationToken>())).ReturnsAsync(manifest);
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
    public async Task InstallAsync_ZipHoldingAnInstaller_ExtractsItAndRunsTheNestedInstaller()
    {
        StageTool("tool.zip", """
            Installers:
            - InstallerType: zip
              NestedInstallerType: nullsoft
              NestedInstallerFiles:
              - RelativeFilePath: setup\tool-setup.exe
            """);
        ArrangeExitCode(0);

        var installed = await CreateSut().InstallAsync(StagedTool);

        installed.Should().BeTrue();
        _files.Verify(f => f.ExtractZip(ToolFolder + @"\tool.zip", ToolFolder + @"\extracted"), Times.Once);
        _processExecutor.Verify(
            p => p.ExecuteAsync(ToolFolder + @"\extracted\setup\tool-setup.exe", "/S", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_PortableZip_InstallsItTheWayWinGetDoes()
    {
        StageTool("tool.zip", PortableZipManifest);
        _registry.Setup(r => r.GetUnexpandedString(EnvironmentKey, "Path")).Returns(@"%SystemRoot%\system32;%SystemRoot%");

        var installed = await CreateSut().InstallAsync(StagedTool);

        installed.Should().BeTrue();
        _files.Verify(f => f.ExtractZip(ToolFolder + @"\tool.zip", ToolRoot), Times.Once);
        _files.Verify(f => f.CreateSymbolicLink(Links + @"\tool.exe", ToolRoot + @"\tool\tool.exe"), Times.Once);
        _registry.Verify(
            r => r.SetValue(EnvironmentKey, "Path", $@"%SystemRoot%\system32;%SystemRoot%;{Links}", RegistryValueKind.ExpandString),
            Times.Once);
        _windowsUI.Verify(w => w.BroadcastEnvironmentChange(), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "WinGetPackageIdentifier", "Example.Tool", RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "DisplayVersion", "2.1.0", RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "InstallLocation", ToolRoot, RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "InstallDirectoryAddedToPath", 0, RegistryValueKind.DWord), Times.Once);
        _processExecutor.Verify(
            p => p.ExecuteAsync("powershell", It.Is<string>(a => a.Contains(CommonPrograms + @"\Tool\tool.lnk") && a.Contains(ToolRoot + @"\tool\tool.exe")), It.IsAny<CancellationToken>()),
            Times.Once);
        _processExecutor.Verify(
            p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_SymlinkRefused_PutsTheInstallFolderOnPath()
    {
        StageTool("tool.zip", PortableZipManifest);
        _files.Setup(f => f.CreateSymbolicLink(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("A required privilege is not held by the client."));

        var installed = await CreateSut().InstallAsync(StagedTool);

        installed.Should().BeTrue();
        _registry.Verify(r => r.SetValue(EnvironmentKey, "Path", ToolRoot, RegistryValueKind.ExpandString), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "InstallDirectoryAddedToPath", 1, RegistryValueKind.DWord), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_LinksAlreadyOnPath_LeavesPathAlone()
    {
        StageTool("tool.zip", PortableZipManifest);
        _registry.Setup(r => r.GetUnexpandedString(EnvironmentKey, "Path")).Returns($@"%SystemRoot%\system32;{Links}\");

        await CreateSut().InstallAsync(StagedTool);

        _registry.Verify(r => r.SetValue(EnvironmentKey, "Path", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _windowsUI.Verify(w => w.BroadcastEnvironmentChange(), Times.Never);
    }

    [Fact]
    public async Task InstallAsync_PortableExe_CopiesItUnderItsCommandAndRecordsItsHash()
    {
        StageTool("tool-2.1.0.exe", """
            PackageIdentifier: Example.Tool
            Installers:
            - InstallerType: portable
              Commands:
              - tool
            """);
        _files.Setup(f => f.ReadAllBytesAsync(ToolRoot + @"\tool.exe", It.IsAny<CancellationToken>())).ReturnsAsync("abc"u8.ToArray());

        var installed = await CreateSut().InstallAsync(StagedTool);

        installed.Should().BeTrue();
        _files.Verify(f => f.CopyFile(ToolFolder + @"\tool-2.1.0.exe", ToolRoot + @"\tool.exe", true), Times.Once);
        _files.Verify(f => f.CreateSymbolicLink(Links + @"\tool.exe", ToolRoot + @"\tool.exe"), Times.Once);
        _registry.Verify(
            r => r.SetValue(ToolUninstallKey, "SHA256", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", RegistryValueKind.String),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_PortableForEveryone_AddsAnAllUsersStartMenuFolder()
    {
        StageTool("tool.zip", PortableZipManifest);

        await CreateSut().InstallAsync(StagedTool);

        _files.Verify(f => f.CreateDirectory(CommonPrograms + @"\Tool"), Times.Once);
        _processExecutor.Verify(
            p => p.ExecuteAsync("powershell", It.Is<string>(a => a.Contains(CommonPrograms + @"\Tool\tool.lnk") && a.Contains(ToolRoot + @"\tool\tool.exe")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_PortableForOnlyMe_InstallsUnderTheUsersProfile()
    {
        StageTool("tool.zip", PortableZipManifest);
        _installOptions.Setup(o => o.EffectiveFor("external-app-tool")).Returns(InstallOptions.Default with { Scope = InstallScope.User });
        _registry.Setup(r => r.GetUnexpandedString(UserEnvironmentKey, "Path")).Returns(@"%USERPROFILE%\bin");

        var installed = await CreateSut().InstallAsync(StagedCatalogTool);

        installed.Should().BeTrue();
        _files.Verify(f => f.ExtractZip(ToolFolder + @"\tool.zip", UserToolRoot), Times.Once);
        _files.Verify(f => f.CreateSymbolicLink(UserLinks + @"\tool.exe", UserToolRoot + @"\tool\tool.exe"), Times.Once);
        _registry.Verify(
            r => r.SetValue(UserEnvironmentKey, "Path", $@"%USERPROFILE%\bin;{UserLinks}", RegistryValueKind.ExpandString),
            Times.Once);
        _registry.Verify(r => r.SetValue(UserToolUninstallKey, "InstallLocation", UserToolRoot, RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _registry.Verify(r => r.SetValue(EnvironmentKey, "Path", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _processExecutor.Verify(
            p => p.ExecuteAsync("powershell", It.Is<string>(a => a.Contains(UserPrograms + @"\Tool\tool.lnk")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_OwnInstallerWithDefaultsForOnlyMe_InstallsUnderTheUsersProfile()
    {
        StageTool("tool.zip", PortableZipManifest);
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default with { Scope = InstallScope.User });

        await CreateSut().InstallAsync(StagedTool);

        _files.Verify(f => f.ExtractZip(ToolFolder + @"\tool.zip", UserToolRoot), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_CatalogAppWithItsOwnEveryoneChoice_IgnoresTheUserDefault()
    {
        StageTool("tool.zip", PortableZipManifest);
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default with { Scope = InstallScope.User });
        _installOptions.Setup(o => o.EffectiveFor("external-app-tool")).Returns(InstallOptions.Default with { Scope = InstallScope.Machine });

        await CreateSut().InstallAsync(StagedCatalogTool);

        _files.Verify(f => f.ExtractZip(ToolFolder + @"\tool.zip", ToolRoot), Times.Once);
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

    private static readonly ItemDefinition Tool = new()
    {
        Id = "external-app-tool",
        Name = "Tool",
        Description = "",
        WinGetPackageId = ["Example.Tool"],
    };

    private void ArrangeInstalledPortable(string uninstallKey, string root, string links, string environmentKey, bool pathAdded)
    {
        var parent = uninstallKey[..uninstallKey.LastIndexOf('\\')];
        _registry.Setup(r => r.GetSubKeyNames(parent)).Returns([uninstallKey[(parent.Length + 1)..]]);
        _registry.Setup(r => r.GetValue(uninstallKey, "WinhanceStagedPortable")).Returns(1);
        _registry.Setup(r => r.GetValue(uninstallKey, "WinGetPackageIdentifier")).Returns("Example.Tool");
        _registry.Setup(r => r.GetValue(uninstallKey, "InstallLocation")).Returns(root);
        _registry.Setup(r => r.GetValue(uninstallKey, "WinhanceLinks")).Returns(links + @"\tool.exe|" + links + @"\tool2.exe");
        if (pathAdded)
            _registry.Setup(r => r.GetValue(uninstallKey, "WinhancePathEntry")).Returns(links);

        _registry.Setup(r => r.GetUnexpandedString(environmentKey, "Path")).Returns($@"%SystemRoot%\system32;{links};C:\Other");
        _files.Setup(f => f.DirectoryExists(root)).Returns(true);
    }

    [Fact]
    public async Task UninstallPortableAsync_ForOnlyMe_RemovesWhatTheInstallLaidDown()
    {
        ArrangeInstalledPortable(UserToolUninstallKey, UserToolRoot, UserLinks, UserEnvironmentKey, pathAdded: true);

        var result = await CreateSut().UninstallPortableAsync(Tool);

        result!.Success.Should().BeTrue();
        _files.Verify(f => f.DeleteFile(UserLinks + @"\tool.exe"), Times.Once);
        _files.Verify(f => f.DeleteFile(UserLinks + @"\tool2.exe"), Times.Once);
        _files.Verify(f => f.DeleteDirectory(UserToolRoot, true), Times.Once);
        _registry.Verify(r => r.SetValue(UserEnvironmentKey, "Path", @"%SystemRoot%\system32;C:\Other", RegistryValueKind.ExpandString), Times.Once);
        _windowsUI.Verify(w => w.BroadcastEnvironmentChange(), Times.Once);
        _registry.Verify(r => r.DeleteKey(UserToolUninstallKey), Times.Once);
        _registry.Verify(r => r.DeleteKey(ToolUninstallKey), Times.Never);
    }

    [Fact]
    public async Task UninstallPortableAsync_ForEveryone_RemovesWhatTheInstallLaidDown()
    {
        ArrangeInstalledPortable(ToolUninstallKey, ToolRoot, Links, EnvironmentKey, pathAdded: true);

        var result = await CreateSut().UninstallPortableAsync(Tool);

        result!.Success.Should().BeTrue();
        _files.Verify(f => f.DeleteFile(Links + @"\tool.exe"), Times.Once);
        _files.Verify(f => f.DeleteDirectory(ToolRoot, true), Times.Once);
        _registry.Verify(r => r.SetValue(EnvironmentKey, "Path", @"%SystemRoot%\system32;C:\Other", RegistryValueKind.ExpandString), Times.Once);
        _registry.Verify(r => r.DeleteKey(ToolUninstallKey), Times.Once);
    }

    [Fact]
    public async Task UninstallPortableAsync_PathEntryWasAlreadyThere_LeavesPathAlone()
    {
        ArrangeInstalledPortable(ToolUninstallKey, ToolRoot, Links, EnvironmentKey, pathAdded: false);

        await CreateSut().UninstallPortableAsync(Tool);

        _registry.Verify(r => r.SetValue(EnvironmentKey, "Path", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _registry.Verify(r => r.DeleteKey(ToolUninstallKey), Times.Once);
    }

    [Fact]
    public async Task UninstallPortableAsync_BothScopesInstalled_RemovesBoth()
    {
        ArrangeInstalledPortable(UserToolUninstallKey, UserToolRoot, UserLinks, UserEnvironmentKey, pathAdded: true);
        ArrangeInstalledPortable(ToolUninstallKey, ToolRoot, Links, EnvironmentKey, pathAdded: true);

        await CreateSut().UninstallPortableAsync(Tool);

        _registry.Verify(r => r.DeleteKey(UserToolUninstallKey), Times.Once);
        _registry.Verify(r => r.DeleteKey(ToolUninstallKey), Times.Once);
    }

    [Fact]
    public async Task UninstallPortableAsync_DeleteFails_KeepsTheEntryAndReportsFailure()
    {
        ArrangeInstalledPortable(ToolUninstallKey, ToolRoot, Links, EnvironmentKey, pathAdded: true);
        _files.Setup(f => f.DeleteDirectory(ToolRoot, true)).Throws(new IOException("in use"));

        var result = await CreateSut().UninstallPortableAsync(Tool);

        result!.Success.Should().BeFalse();
        _registry.Verify(r => r.DeleteKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UninstallPortableAsync_EntryWinGetWrote_IsNotOurs()
    {
        var parent = ToolUninstallKey[..ToolUninstallKey.LastIndexOf('\\')];
        _registry.Setup(r => r.GetSubKeyNames(parent)).Returns(["Example.Tool_Microsoft.Winget.Source_8wekyb3d8bbwe"]);
        _registry.Setup(r => r.GetValue(ToolUninstallKey, "WinGetPackageIdentifier")).Returns("Example.Tool");

        var result = await CreateSut().UninstallPortableAsync(Tool);

        result.Should().BeNull();
        _files.Verify(f => f.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task InstallAsync_PortableZip_MarksTheEntryAndRecordsWhatItAdded()
    {
        StageTool("tool.zip", PortableZipManifest);

        await CreateSut().InstallAsync(StagedTool);

        _registry.Verify(r => r.SetValue(ToolUninstallKey, "WinhanceStagedPortable", 1, RegistryValueKind.DWord), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "WinhanceLinks", Links + @"\tool.exe", RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(ToolUninstallKey, "WinhancePathEntry", Links, RegistryValueKind.String), Times.Once);
    }
}
