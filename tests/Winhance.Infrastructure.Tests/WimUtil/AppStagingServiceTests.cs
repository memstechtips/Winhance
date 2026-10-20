using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil;

public class AppStagingServiceTests : IDisposable
{
    private const string Vlc = "external-app-vlc";
    // ExplorerPatcher has a winget id and a catalog download URL; Sniffnet has only the URLs.
    private const string ExplorerPatcher = "external-app-explorerpatcher";
    private const string Sniffnet = "external-app-sniffnet";

    private const string WixManifest = """
        ManifestType: merged
        Installers:
        - InstallerType: wix
          Scope: machine
          InstallerSwitches:
            Silent: /quiet /norestart
        """;

    private const string MsixManifest = """
        ManifestType: merged
        Installers:
        - InstallerType: msix
        """;

    private static readonly SettingChoice[] OnlyX64 =
    [
        new("autounattend-architecture-x86", new ChoiceValue.CheckBox(false)),
        new("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false)),
    ];

    private static readonly SettingChoice[] OnlyArm64 =
    [
        new("autounattend-architecture-x86", new ChoiceValue.CheckBox(false)),
        new("autounattend-architecture-x64", new ChoiceValue.CheckBox(false)),
    ];

    private static readonly SettingChoice[] OnlyX86 =
    [
        new("autounattend-architecture-x64", new ChoiceValue.CheckBox(false)),
        new("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false)),
    ];

    private readonly Mock<IWinGetPackageInstaller> _winGet = new();
    private readonly Mock<IDirectDownloadService> _downloads = new();
    private readonly Mock<IVersionService> _versions = new();
    private readonly string _root;
    private readonly string _media;
    private readonly string _apps;
    private readonly AppStagingService _sut;

    public AppStagingServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
        _media = Path.Combine(_root, "media");
        _apps = Path.Combine(_media, @"sources\$OEM$\$1\ProgramData\Winhance\Unattend\Apps");
        Directory.CreateDirectory(_media);
        _versions.Setup(v => v.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VersionInfo());

        _sut = new AppStagingService(
            _winGet.Object,
            _downloads.Object,
            new FileSystemService(),
            _versions.Object,
            Mock.Of<ILogService>(),
            Mock.Of<ILocalizationService>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static SelectionSet Set(SettingChoice[] architectures, params string[] appIds) =>
        SelectionSet.Empty with
        {
            Settings = architectures,
            ExternalApps = [.. appIds.Select(id => new AppChoice(id, id, null, null, null, null))],
        };

    private static SelectionSet WithOwnInstallers(SelectionSet set, params OwnInstaller[] installers) => set with
    {
        Settings =
        [
            .. set.Settings,
            new SettingChoice(OwnInstaller.SettingId, new ChoiceValue.List(
            [
                .. installers.Select(installer => new ChoiceValue.ListRow(new Dictionary<string, string>
                {
                    ["file"] = installer.FilePath,
                    ["switches"] = installer.SilentSwitches,
                })),
            ])),
        ],
    };

    // Every run stages the Winhance installer, which has its own test; these assertions are about the apps
    // the user picked, so it stays out of them.
    private static string[] NotStaged(IReadOnlyList<StagedApp> outcomes) =>
        [.. outcomes.Where(app => !app.Staged && app.Name != "Winhance").Select(app => app.Name)];

    private static string[] FileNames(string folder) => [.. Directory.GetFiles(folder).Select(path => Path.GetFileName(path))];

    private static string[] FolderNames(string folder) => [.. Directory.GetDirectories(folder).Select(path => Path.GetFileName(path))];

    private void WinGetDownloads(bool forAllUsers, string installerName, string manifest)
    {
        _winGet
            .Setup(w => w.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), forAllUsers, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, string, string?, CancellationToken>((_, _, _, folder, _, _) =>
            {
                File.WriteAllText(Path.Combine(folder, installerName), "installer");
                File.WriteAllText(Path.Combine(folder, Path.ChangeExtension(installerName, ".yaml")), manifest);
            })
            .ReturnsAsync(true);
    }

    private void UrlDownloads(string fileName)
    {
        _downloads
            .Setup(d => d.DownloadAsync(It.IsAny<ItemDefinition>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((ItemDefinition _, string _, string folder, IProgress<TaskProgressDetail>? _, CancellationToken _) =>
            {
                var path = Path.Combine(folder, fileName);
                File.WriteAllText(path, "installer");
                return Task.FromResult<string?>(path);
            });
    }

    [Fact]
    public async Task StageAsync_MachineWideDownloadWorks_StagesItAndTriesNothingElse()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, Vlc)));

        notStaged.Should().BeEmpty();
        FileNames(Path.Combine(_apps, Vlc, "x64")).Should().BeEquivalentTo("vlc.msi", "vlc.yaml");
        _winGet.Verify(w => w.DownloadPackageAsync("VideoLAN.VLC", "x64", true, Path.Combine(_apps, Vlc, "x64"), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _downloads.Verify(d => d.DownloadAsync(It.IsAny<ItemDefinition>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_MachineWideDownloadFails_FallsBackToTheDefaultScope()
    {
        WinGetDownloads(forAllUsers: false, "vlc.msi", WixManifest);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, Vlc)));

        notStaged.Should().BeEmpty();
        FileNames(Path.Combine(_apps, Vlc, "x64")).Should().BeEquivalentTo("vlc.msi", "vlc.yaml");
        _winGet.Verify(w => w.DownloadPackageAsync("VideoLAN.VLC", "x64", true, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _winGet.Verify(w => w.DownloadPackageAsync("VideoLAN.VLC", "x64", false, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAsync_WinGetGivesAnMsix_StagesTheCatalogDownloadInstead()
    {
        WinGetDownloads(forAllUsers: true, "app.msix", MsixManifest);
        WinGetDownloads(forAllUsers: false, "app.msix", MsixManifest);
        UrlDownloads("ep_setup.exe");

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, ExplorerPatcher)));

        notStaged.Should().BeEmpty();
        FileNames(Path.Combine(_apps, ExplorerPatcher, "x64")).Should().Equal("ep_setup.exe");
        _downloads.Verify(d => d.DownloadAsync(It.Is<ItemDefinition>(a => a.Id == ExplorerPatcher), "x64", Path.Combine(_apps, ExplorerPatcher, "x64"), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAsync_AppWithNoWinGetId_DownloadsItsUrlForTheTickedArchitecture()
    {
        UrlDownloads("sniffnet.msi");

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyArm64, Sniffnet)));

        notStaged.Should().BeEmpty();
        FileNames(Path.Combine(_apps, Sniffnet, "arm64")).Should().Equal("sniffnet.msi");
        _downloads.Verify(d => d.DownloadAsync(It.Is<ItemDefinition>(a => a.Id == Sniffnet), "arm64", Path.Combine(_apps, Sniffnet, "arm64"), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_NothingDownloads_ReturnsTheNameAndLeavesNoFolder()
    {
        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, Vlc)));

        notStaged.Should().Equal("VLC media player");
        Directory.Exists(Path.Combine(_apps, Vlc)).Should().BeFalse();
    }

    [Fact]
    public async Task StageAsync_IdNotInTheCatalog_ReturnsItsName()
    {
        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, "external-app-gone")));

        notStaged.Should().Equal("external-app-gone");
    }

    [Fact]
    public async Task StageAsync_WinGetSearchPick_StagesItsPackageInAFolderNamedAfterItsId()
    {
        WinGetDownloads(forAllUsers: true, "app.msi", WixManifest);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, "winget-Contoso.App")));

        notStaged.Should().BeEmpty();
        FileNames(Path.Combine(_apps, "winget-Contoso.App", "x64")).Should().BeEquivalentTo("app.msi", "app.yaml");
        _winGet.Verify(w => w.DownloadPackageAsync("Contoso.App", "x64", true, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAsync_FolderOfAnUncheckedApp_IsDeleted()
    {
        var stale = Path.Combine(_apps, "external-app-old", "x64");
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "old.exe"), "installer");
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);

        await _sut.StageAsync(_media, Set(OnlyX64, Vlc));

        FolderNames(_apps).Should().Equal(Vlc);
    }

    [Fact]
    public async Task StageAsync_OnlyArm64Ticked_StagesOnlyArm64()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);

        await _sut.StageAsync(_media, Set(OnlyArm64, Vlc));

        FolderNames(Path.Combine(_apps, Vlc)).Should().Equal("arm64");
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), It.Is<string>(a => a != "arm64"), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_ArchitectureCardsUntouched_StagesX64AndArm64()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);

        await _sut.StageAsync(_media, Set([], Vlc));

        FolderNames(Path.Combine(_apps, Vlc)).Should().BeEquivalentTo("arm64", "x64");
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), "x86", It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_OnlyX86Ticked_StagesNothingAndReportsTheApp()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX86, Vlc)));

        notStaged.Should().Equal("VLC media player");
        Directory.Exists(Path.Combine(_apps, Vlc)).Should().BeFalse();
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_Arm64FailsWhileX64Stages_DoesNotReportTheApp()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);
        _winGet
            .Setup(w => w.DownloadPackageAsync(It.IsAny<string>(), "arm64", It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set([], Vlc)));

        notStaged.Should().BeEmpty();
        FolderNames(Path.Combine(_apps, Vlc)).Should().Equal("x64");
    }

    [Fact]
    public async Task StageAsync_AppAlreadyStaged_DoesNotDownloadAgain()
    {
        var x64 = Path.Combine(_apps, Vlc, "x64");
        Directory.CreateDirectory(x64);
        File.WriteAllText(Path.Combine(x64, "vlc.msi"), "installer");
        File.WriteAllText(Path.Combine(x64, "vlc.yaml"), WixManifest);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, Vlc)));

        notStaged.Should().BeEmpty();
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAsync_OwnInstallerWithSwitches_GetsAManifestTheReaderReadsBack()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);
        var setup = Path.Combine(source, "Setup.exe");
        File.WriteAllText(setup, "installer");

        var notStaged = NotStaged(await _sut.StageAsync(_media, WithOwnInstallers(Set(OnlyX64), new OwnInstaller(setup, "/s /v\"/qn\""))));

        notStaged.Should().BeEmpty();
        var folder = Path.Combine(_apps, "Custom", "Setup");
        FileNames(folder).Should().BeEquivalentTo("Setup.exe", "Setup.yaml");
        var manifest = WinGetManifest.TryRead(File.ReadAllText(Path.Combine(folder, "Setup.yaml")));
        manifest.Should().NotBeNull();
        manifest!.InstallerType.Should().Be("exe");
        manifest.Silent.Should().Be("/s /v\"/qn\"");
    }

    [Fact]
    public async Task StageAsync_OwnInstallers_SameNameGetsItsOwnFolderAndAMissingFileIsReported()
    {
        var first = Path.Combine(_root, "one", "Setup.exe");
        var second = Path.Combine(_root, "two", "Setup.exe");
        foreach (var path in new[] { first, second })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "installer");
        }

        var notStaged = NotStaged(await _sut.StageAsync(
            _media,
            WithOwnInstallers(Set(OnlyX64), new(first, ""), new(second, ""), new(Path.Combine(_root, "Gone.exe"), ""))));

        notStaged.Should().Equal("Gone.exe");
        FileNames(Path.Combine(_apps, "Custom", "Setup")).Should().Equal("Setup.exe");
        FileNames(Path.Combine(_apps, "Custom", "Setup (2)")).Should().Equal("Setup.exe");
    }

    [Fact]
    public async Task StageAsync_OwnInstallerGoneFromThisPc_StaysOnTheMediaFromAnEarlierRun()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);
        var setup = Path.Combine(source, "Setup.exe");
        File.WriteAllText(setup, "installer");
        var set = WithOwnInstallers(Set(OnlyX64), new OwnInstaller(setup, ""));
        await _sut.StageAsync(_media, set);
        File.Delete(setup);

        var notStaged = NotStaged(await _sut.StageAsync(_media, set));

        notStaged.Should().BeEmpty();
        FileNames(Path.Combine(_apps, "Custom", "Setup")).Should().Equal("Setup.exe");
    }

    [Fact]
    public async Task StageAsync_OwnInstallerGoneFromThisPc_GetsItsChangedSwitchesOnTheMedia()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);
        var setup = Path.Combine(source, "Setup.exe");
        File.WriteAllText(setup, "installer");
        await _sut.StageAsync(_media, WithOwnInstallers(Set(OnlyX64), new OwnInstaller(setup, "/old")));
        File.Delete(setup);
        var manifest = Path.Combine(_apps, "Custom", "Setup", "Setup.yaml");

        var notStaged = NotStaged(await _sut.StageAsync(_media, WithOwnInstallers(Set(OnlyX64), new OwnInstaller(setup, "/new"))));

        notStaged.Should().BeEmpty();
        WinGetManifest.TryRead(File.ReadAllText(manifest))!.Silent.Should().Be("/new");

        await _sut.StageAsync(_media, WithOwnInstallers(Set(OnlyX64), new OwnInstaller(setup, "")));

        FileNames(Path.Combine(_apps, "Custom", "Setup")).Should().Equal("Setup.exe");
    }

    [Fact]
    public async Task StageAsync_OneOfTwoSameNamedInstallersGone_IsReportedRatherThanMistakenForTheOther()
    {
        var first = Path.Combine(_root, "one", "Setup.exe");
        var second = Path.Combine(_root, "two", "Setup.exe");
        foreach (var path in new[] { first, second })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, path);
        }

        var set = WithOwnInstallers(Set(OnlyX64), new OwnInstaller(first, ""), new OwnInstaller(second, ""));
        await _sut.StageAsync(_media, set);
        File.Delete(second);

        var notStaged = NotStaged(await _sut.StageAsync(_media, set));

        notStaged.Should().Equal("Setup.exe");
        File.ReadAllText(Path.Combine(_apps, "Custom", "Setup", "Setup.exe")).Should().Be(first);
        Directory.Exists(Path.Combine(_apps, "Custom", "Setup (2)")).Should().BeFalse();
    }

    [Fact]
    public async Task StageAsync_IdThatWouldLeaveTheAppsFolder_IsReportedAndNothingIsDownloaded()
    {
        const string id = @"winget-..\..\Evil";
        WinGetDownloads(forAllUsers: true, "app.msi", WixManifest);

        var notStaged = NotStaged(await _sut.StageAsync(_media, Set(OnlyX64, id)));

        notStaged.Should().Equal(id);
        Directory.Exists(Path.GetFullPath(Path.Combine(_apps, id))).Should().BeFalse();
        _winGet.Verify(w => w.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(@"..\Evil")]
    [InlineData(@"winget-..\..\Evil")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData(@"C:\Evil")]
    [InlineData("winget-Contoso.App/x64")]
    public void IsDirectChild_RefusesANameThatIsNotOneFolderInside(string name)
    {
        AppStagingService.IsDirectChild(_apps, name).Should().BeFalse();
    }

    [Theory]
    [InlineData("winget-Contoso.App")]
    [InlineData("external-app-vlc")]
    public void IsDirectChild_AcceptsAnAppId(string name)
    {
        AppStagingService.IsDirectChild(_apps, name).Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_WinhanceInstaller_LandsOnTheMediaOnce_WhenGitHubIsOutOfReach()
    {
        WinhanceDownloads("installer");

        await _sut.StageAsync(_media, Set(OnlyX64));
        await _sut.StageAsync(_media, Set(OnlyX64));

        File.ReadAllText(MediaInstaller).Should().Be("installer");
        _downloads.Verify(d => d.DownloadFileAsync(VersionService.LatestInstallerUrl, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAsync_WinhanceInstallerOlderThanTheLatestRelease_IsReplaced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MediaInstaller)!);
        File.WriteAllText(MediaInstaller, "old");
        _versions.Setup(v => v.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(VersionInfo.FromTag("v26.10.01"));
        WinhanceDownloads("new");

        var outcomes = await _sut.StageAsync(_media, Set(OnlyX64));

        File.ReadAllText(MediaInstaller).Should().Be("new");
        outcomes.Should().ContainSingle(o => o.Name == "Winhance").Which.Staged.Should().BeTrue();
    }

    [Fact]
    public async Task StageAsync_WinhanceInstallerNotOlderThanTheLatestRelease_IsKept()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MediaInstaller)!);
        File.WriteAllText(MediaInstaller, "dev build");
        _versions.Setup(v => v.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VersionInfo { Version = "v00.00.00" });

        await _sut.StageAsync(_media, Set(OnlyX64));

        File.ReadAllText(MediaInstaller).Should().Be("dev build");
        _downloads.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StageAsync_WinhanceInstallerReplacementFails_KeepsTheOldCopy()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MediaInstaller)!);
        File.WriteAllText(MediaInstaller, "old");
        _versions.Setup(v => v.GetLatestReleaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(VersionInfo.FromTag("v26.10.01"));

        var outcomes = await _sut.StageAsync(_media, Set(OnlyX64));

        File.ReadAllText(MediaInstaller).Should().Be("old");
        outcomes.Should().ContainSingle(o => o.Name == "Winhance").Which.Staged.Should().BeTrue();
    }

    [Theory]
    [InlineData("26.09.28", "v26.09.28")]
    [InlineData("26.09.28-beta", "v26.09.28-beta")]
    [InlineData("v26.09.28+3f2a1b", "v26.09.28")]
    [InlineData("", "")]
    public void FromProductVersion_ReadsTheInstallersVersion(string productVersion, string expected)
    {
        VersionInfo.FromProductVersion(productVersion).Version.Should().Be(expected);
    }

    private string MediaInstaller => Path.Combine(_media, @"sources\$OEM$\$1\ProgramData\Winhance\Unattend\Winhance.Installer.exe");

    private void WinhanceDownloads(string content) =>
        _downloads
            .Setup(d => d.DownloadFileAsync(VersionService.LatestInstallerUrl, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string folder, string _, IProgress<TaskProgressDetail>? _, CancellationToken _) =>
            {
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "Winhance.Installer.exe");
                File.WriteAllText(path, content);
                return Task.FromResult<string?>(path);
            });

    [Fact]
    public async Task StageAsync_ReportsEveryItemItTried_NotOnlyTheFailures()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);
        var setup = Path.Combine(source, "Setup.exe");
        File.WriteAllText(setup, "installer");

        var outcomes = await _sut.StageAsync(
            _media,
            WithOwnInstallers(Set(OnlyX64, Vlc), new OwnInstaller(setup, "")));

        outcomes.Should().BeEquivalentTo(new[]
        {
            new StagedApp("VLC media player", true),
            new StagedApp("Setup.exe", true),
            new StagedApp("Winhance", false),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task StageAsync_TellsWinGetTheAppsName_SoTheTerminalNamesItRatherThanItsPackageId()
    {
        WinGetDownloads(forAllUsers: true, "vlc.msi", WixManifest);

        await _sut.StageAsync(_media, Set(OnlyX64, Vlc));

        _winGet.Verify(
            w => w.DownloadPackageAsync("VideoLAN.VLC", "x64", true, It.IsAny<string>(), "VLC media player", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RemoveStaged_DeletesTheAppsAndKeepsTheWinhanceInstaller()
    {
        Directory.CreateDirectory(Path.Combine(_apps, Vlc, "x64"));
        var installer = Path.Combine(_media, @"sources\$OEM$\$1\ProgramData\Winhance\Unattend\Winhance.Installer.exe");
        File.WriteAllText(installer, "installer");

        _sut.RemoveStaged(_media);

        Directory.Exists(_apps).Should().BeFalse();
        File.Exists(installer).Should().BeTrue();
    }
}
