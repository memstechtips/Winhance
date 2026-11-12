using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflineServicingServiceTests
{
    private const string Work = @"D:\iso\WinhanceWIM";
    private const string Wim = Work + @"\sources\install.wim";
    private const string Esd = Work + @"\sources\install.esd";
    private const string Sxs = Work + @"\sources\sxs";
    private const string Oem = Work + @"\sources\$OEM$\$$\Drivers";
    private const string MountDir = @"D:\iso\WinhanceMount";
    private const string OemRoot = Work + @"\sources\$OEM$";
    private const string AppsParent = OemRoot + @"\$1\ProgramData\Winhance\Unattend";
    private const string Checkpoint = "windows11.0-kb5043080-x64.msu";
    private const string Target = "windows11.0-kb5062553-x64.msu";
    private const string Older = "windows11.0-kb5036893-x64.msu";
    private const int PackageNotApplicable = unchecked((int)0x800f081e);

    private static readonly string UpdateDir = Work + "\\" + ScriptPaths.UnattendUpdatesMediaDirectory;
    private static readonly string TargetRecord = UpdateDir + UpdateStaging.TargetRecordSuffix;

    private readonly Mock<IDismProcessRunner> _dism = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IDriverInstallStepWriter> _driverStep = new();
    private readonly Mock<IUpdateInstallStepWriter> _updateStep = new();
    private readonly Mock<IWindowsUpdateStager> _stager = new();
    private readonly Mock<IWimImageService> _wimImages = new();
    private readonly Mock<IOfflineAppRemover> _appRemover = new();
    private readonly Mock<IOfflineSettingsApplier> _settings = new();
    private readonly List<string> _events = [];
    private readonly List<(string Args, CancellationToken Token)> _calls = [];
    private Func<string, int> _exit = _ => 0;

    public OfflineServicingServiceTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _files.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string p) => p.Split('\\').Last());
        _files.Setup(f => f.GetDirectoryName(It.IsAny<string>()))
            .Returns((string p) => p.Contains('\\') ? p[..p.LastIndexOf('\\')] : null);
        _files.Setup(f => f.GetTempPath()).Returns(@"C:\Temp");
        _files.Setup(f => f.FileExists(Wim)).Returns(true);
        _files.Setup(f => f.CreatePrivateDirectory(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1000L);
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ReturnsAsync(Image(ImageFormat.Wim, Wim, "Windows 11 Pro"));
        _wimImages
            .Setup(w => w.KeepEditionAsWimAsync(Work, It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _events.Add("prepare"))
            .ReturnsAsync(new EditionRemovalResult(true));
        _wimImages
            .Setup(w => w.RewriteAsync(Work, It.IsAny<ImageFormat>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _events.Add("export"))
            .ReturnsAsync(new EditionRemovalResult(true));
        _appRemover
            .Setup(r => r.RemoveAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineStepOutcome(true, [], 1));
        _settings
            .Setup(r => r.ApplyAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineStepOutcome(true, [], 1));
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _dism.Setup(r => r.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(true);
        _dism
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string args, IProgress<TaskProgressDetail>? _, CancellationToken token) =>
            {
                _calls.Add((args, token));
                _events.Add(args);
                return Task.FromResult((_exit(args), string.Empty));
            });
    }

    private OfflineServicingService Sut() => new(
        _dism.Object, _files.Object, _localization.Object, new Mock<ILogService>().Object,
        _driverStep.Object, _updateStep.Object, _stager.Object, _wimImages.Object, _appRemover.Object, _settings.Object);

    private void StageDrivers()
    {
        _files.Setup(f => f.DirectoryExists(Oem)).Returns(true);
        _files.Setup(f => f.GetFiles(Oem, "*", SearchOption.AllDirectories)).Returns([Oem + @"\net\net.inf"]);
    }

    // The last name is the recorded target, as the stager writes it.
    private void StageUpdate(params string[] names)
    {
        _files.Setup(f => f.DirectoryExists(UpdateDir)).Returns(true);
        _files.Setup(f => f.GetFiles(UpdateDir, "*.msu", It.IsAny<SearchOption>()))
            .Returns(names.Select(n => UpdateDir + "\\" + n).ToArray());
        foreach (var name in names)
            _files.Setup(f => f.FileExists(UpdateDir + "\\" + name)).Returns(true);
        _files.Setup(f => f.FileExists(TargetRecord)).Returns(true);
        _files.Setup(f => f.ReadAllText(TargetRecord)).Returns(names[^1] + "\r\n");
    }

    private static ImageFormatInfo Image(ImageFormat format, string path, params string[] names) => new()
    {
        Format = format,
        FilePath = path,
        Editions = names.Select((n, i) => new ImageEdition(i + 1, n, "Professional")).ToList(),
    };

    private static OfflineBuildRequest Req() => new(Work);

    private static SelectionSet Apps() => new([], [new AppChoice("app", "App", ["Contoso.App"], null, null, null)], []);

    private static SelectionSet Tweaks() => new([new SettingChoice("setting", new ChoiceValue.Toggle(true))], [], []);

    private IEnumerable<string> Args => _calls.Select(c => c.Args);

    [Fact]
    public async Task ServiceAsync_DriversAndUpdate_RunsDismInOrder()
    {
        StageDrivers();
        StageUpdate(Checkpoint, Target);

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        Args.Should().SatisfyRespectively(
            a => a.Should().Be("/Cleanup-Mountpoints"),
            a => a.Should().Be($"/Mount-Image /ImageFile:\"{Wim}\" /Index:1 /MountDir:\"{MountDir}\""),
            a => a.Should().StartWith($"/Image:\"{MountDir}\" /Add-Package /PackagePath:"),
            a => a.Should().Be($"/Image:\"{MountDir}\" /Add-Driver /Driver:\"{Oem}\" /Recurse"),
            a => a.Should().Be($"/Image:\"{MountDir}\" /Cleanup-Image /StartComponentCleanup"),
            a => a.Should().Be($"/Unmount-Image /MountDir:\"{MountDir}\" /Commit"));
    }

    [Fact]
    public async Task ServiceAsync_ReadOnlyImage_ClearsTheFlagAfterCleanupAndBeforeTheMount()
    {
        StageDrivers();
        var dismCallsAtClear = -1;
        _files.Setup(f => f.SetFileAttributes(Wim, FileAttributes.Normal)).Callback(() => dismCallsAtClear = _calls.Count);

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        dismCallsAtClear.Should().Be(1);
        Args.ElementAt(1).Should().StartWith("/Mount-Image");
    }

    [Fact]
    public async Task ServiceAsync_UpdateTargetIsTheRecordedOneNotTheHighestKb()
    {
        StageUpdate(Checkpoint, Target, Older);

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        Args.Where(a => a.Contains("/Add-Package")).Should().ContainSingle()
            .Which.Should().EndWith($"/PackagePath:\"{UpdateDir}\\{Older}\"");
    }

    [Fact]
    public async Task ServiceAsync_UpdateFolderWithNoRecord_CountsAsNotStaged()
    {
        StageUpdate(Checkpoint, Target);
        _files.Setup(f => f.FileExists(TargetRecord)).Returns(false);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.NotStaged), o => o.Excluding(r => r.Phases));
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_RecordNamesAMissingFile_CountsAsNotStaged()
    {
        StageUpdate(Checkpoint, Target);
        _files.Setup(f => f.FileExists(UpdateDir + "\\" + Target)).Returns(false);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.NotStaged);
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_MoreThanOneEditionAndNoneChosen_FallsBackWithoutMounting()
    {
        StageDrivers();
        StageUpdate(Target);
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ReturnsAsync(Image(ImageFormat.Wim, Wim, "Windows 11 Home", "Windows 11 Pro"));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup), o => o.Excluding(r => r.Phases));
        _calls.Should().BeEmpty();
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_ImageCannotBeRead_FallsBackWithoutMounting()
    {
        StageUpdate(Target);
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ThrowsAsync(new InvalidOperationException("The image is in use."));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.FellBackToSetup);
        _calls.Should().BeEmpty();
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_AddPackageFails_DiscardsAndEnsuresBothSteps()
    {
        StageDrivers();
        StageUpdate(Checkpoint, Target);
        _exit = a => a.Contains("/Add-Package") ? PackageNotApplicable : 0;

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup), o => o.Excluding(r => r.Phases));
        Args.Last().Should().Be($"/Unmount-Image /MountDir:\"{MountDir}\" /Discard");
        Args.Should().NotContain(a => a.Contains("/Commit"));
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _files.Verify(f => f.DeleteDirectory(Oem, It.IsAny<bool>()), Times.Never);
        _stager.Verify(s => s.RemoveStaged(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ServiceAsync_AddDriverFails_CommitsUpdateAndKeepsDriverStep()
    {
        StageDrivers();
        StageUpdate(Target);
        _exit = a => a.Contains("/Add-Driver") ? 2 : 0;

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.Added), o => o.Excluding(r => r.Phases));
        Args.Should().Contain(a => a.EndsWith("/Commit"));
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _driverStep.Verify(d => d.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _files.Verify(f => f.DeleteDirectory(Oem, It.IsAny<bool>()), Times.Never);
        _stager.Verify(s => s.RemoveStaged(Work), Times.Once);
        _updateStep.Verify(u => u.RemoveAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_Exit3010_CountsAsAdded()
    {
        StageUpdate(Target);
        _exit = a => a.Contains("/Add-Package") ? 3010 : 0;

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.Added), o => o.Excluding(r => r.Phases));
        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_AddDriverExit3010_CountsAsAdded()
    {
        StageDrivers();
        _exit = a => a.Contains("/Add-Driver") ? 3010 : 0;

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Drivers.Should().Be(ServicedItemOutcome.Added);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.Drivers, ServicingPhaseState.Done));
    }

    [Fact]
    public async Task ServiceAsync_MountFails_AddsNothingAndEnsuresTheSteps()
    {
        StageDrivers();
        StageUpdate(Target);
        _exit = a => a.Contains("/Mount-Image") ? 1 : 0;

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.AnyFellBack.Should().BeTrue();
        Args.Should().NotContain(a => a.Contains("/Add-Driver") || a.Contains("/Add-Package") || a.Contains("/Commit"));
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_Cancelled_DiscardsWithNoneTokenAndRethrows()
    {
        StageUpdate(Target);
        _exit = a => a.Contains("/Add-Package") ? throw new OperationCanceledException() : 0;
        using var cts = new CancellationTokenSource();

        var act = () => Sut().ServiceAsync(Req(), null, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _calls.Last().Should().Be(($"/Unmount-Image /MountDir:\"{MountDir}\" /Discard", CancellationToken.None));
        Args.Should().NotContain(a => a.Contains("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_Commit_IgnoresTheCancellationToken()
    {
        StageUpdate(Target);
        using var cts = new CancellationTokenSource();

        await Sut().ServiceAsync(Req(), null, null, cts.Token);

        _calls.Single(c => c.Args.EndsWith("/Commit")).Token.Should().Be(CancellationToken.None);
    }

    [Fact]
    public async Task ServiceAsync_CancelledBeforeCommit_DiscardsInsteadOfCommitting()
    {
        StageUpdate(Target);
        using var cts = new CancellationTokenSource();
        _exit = a =>
        {
            if (a.Contains("/StartComponentCleanup"))
                cts.Cancel();
            return 0;
        };

        var act = () => Sut().ServiceAsync(Req(), null, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Args.Should().NotContain(a => a.Contains("/Commit"));
        Args.Last().Should().Be($"/Unmount-Image /MountDir:\"{MountDir}\" /Discard");
    }

    [Fact]
    public async Task ServiceAsync_NotEnoughSpace_FallsBackWithoutMounting()
    {
        StageDrivers();
        StageUpdate(Target);
        _dism.Setup(r => r.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
            .ThrowsAsync(new InsufficientDiskSpaceException("D:", 20, 5, "Offline servicing"));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup), o => o.Excluding(r => r.Phases));
        _calls.Should().BeEmpty();
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_MountDirCannotBeMadePrivate_FallsBackToSetupWithoutMounting()
    {
        StageUpdate(Target);
        _files.Setup(f => f.CreatePrivateDirectory(MountDir)).Returns(false);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.FellBackToSetup);
        Args.Should().NotContain(a => a.Contains("/Mount-Image"));
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_MountDirStillHoldsFiles_LeavesThemAndFallsBack()
    {
        StageUpdate(Target);
        _files.Setup(f => f.DirectoryExists(MountDir)).Returns(true);
        _files.Setup(f => f.GetFiles(MountDir, It.IsAny<string>(), It.IsAny<SearchOption>())).Returns([MountDir + @"\Windows"]);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.FellBackToSetup);
        Args.First().Should().Be($"/Unmount-Image /MountDir:\"{MountDir}\" /Discard");
        Args.Should().NotContain(a => a.Contains("/Mount-Image"));
        _files.Verify(f => f.DeleteDirectory(MountDir, It.IsAny<bool>()), Times.Never);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_Success_RemovesStagedFoldersAndSteps()
    {
        StageDrivers();
        StageUpdate(Target);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added), o => o.Excluding(r => r.Phases));
        result.AnyFellBack.Should().BeFalse();
        _files.Verify(f => f.DeleteDirectory(Oem, true), Times.Once);
        _stager.Verify(s => s.RemoveStaged(Work), Times.Once);
        _driverStep.Verify(d => d.RemoveAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.RemoveAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _driverStep.Verify(d => d.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _updateStep.Verify(u => u.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ServiceAsync_DriversAdded_RemovesTheStepBeforeDeletingTheReadOnlyDrivers()
    {
        StageDrivers();
        var order = new List<string>();
        _driverStep.Setup(d => d.RemoveAsync(Work, It.IsAny<CancellationToken>())).Callback(() => order.Add("step")).ReturnsAsync(true);
        _files.Setup(f => f.SetFileAttributes(Oem + @"\net\net.inf", FileAttributes.Normal)).Callback(() => order.Add("writable"));
        _files.Setup(f => f.DeleteDirectory(Oem, true)).Callback(() => order.Add("delete"));

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        order.Should().Equal("step", "writable", "delete");
    }

    [Fact]
    public async Task ServiceAsync_DriverStepCannotBeRemoved_KeepsTheDrivers()
    {
        StageDrivers();
        _driverStep.Setup(d => d.RemoveAsync(Work, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("autounattend.xml is in use"));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Drivers.Should().Be(ServicedItemOutcome.Added);
        _files.Verify(f => f.DeleteDirectory(Oem, It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ServiceAsync_Success_PrunesEmptiedOemFoldersButKeepsStagedApps()
    {
        StageDrivers();
        StageUpdate(Target);
        _files.Setup(f => f.DirectoryExists(It.Is<string>(p => p.StartsWith(OemRoot, StringComparison.Ordinal)))).Returns(true);
        _files.Setup(f => f.GetDirectories(AppsParent, It.IsAny<string>(), It.IsAny<SearchOption>())).Returns([AppsParent + @"\Apps"]);
        _files.Setup(f => f.GetDirectories(It.Is<string>(p => p.Length < AppsParent.Length && AppsParent.StartsWith(p, StringComparison.Ordinal)), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns((string p, string _, SearchOption _) => new[] { ChildOnTheWay(p) });

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        _files.Verify(f => f.DeleteDirectory(OemRoot + @"\$$", It.IsAny<bool>()), Times.Once);
        _files.Verify(f => f.DeleteDirectory(It.Is<string>(p => AppsParent.StartsWith(p, StringComparison.Ordinal)), It.IsAny<bool>()), Times.Never);
    }

    private static string ChildOnTheWay(string parent)
    {
        var next = AppsParent.IndexOf('\\', parent.Length + 1);
        return next < 0 ? AppsParent : AppsParent[..next];
    }

    [Fact]
    public async Task ServiceAsync_WorkingFolderOnAShare_FallsBackWithoutDism()
    {
        StageDrivers();
        StageUpdate(Target);
        _files.Setup(f => f.GetPathRoot(It.IsAny<string>())).Returns(@"\\nas\iso");

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup), o => o.Excluding(r => r.Phases));
        _calls.Should().BeEmpty();
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_NothingStaged_RunsNoDism()
    {
        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Should().BeEquivalentTo(new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.NotStaged), o => o.Excluding(r => r.Phases));
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_NoInstallWim_Throws()
    {
        _files.Setup(f => f.FileExists(Wim)).Returns(false);
        StageUpdate(Target);

        var act = () => Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("WIMUtil_Msg_ServicingNeedsWim");
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_MultiEditionWim_KeepsTheChosenEditionBeforeMounting()
    {
        StageUpdate(Target);
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ReturnsAsync(Image(ImageFormat.Wim, Wim, "Windows 11 Home", "Windows 11 Pro"));

        var result = await Sut().ServiceAsync(Req() with { EditionIndex = 2 }, null, null, CancellationToken.None);

        _wimImages.Verify(w => w.KeepEditionAsWimAsync(Work, "Windows 11 Pro", It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.IndexOf("prepare").Should().BeLessThan(_events.FindIndex(e => e.StartsWith("/Mount-Image")));
        result.Update.Should().Be(ServicedItemOutcome.Added);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.PrepareImage, ServicingPhaseState.Done));
    }

    [Fact]
    public async Task ServiceAsync_EsdSource_IsExportedToWimBeforeMounting()
    {
        StageUpdate(Target);
        _files.Setup(f => f.FileExists(Esd)).Returns(true);
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ReturnsAsync(Image(ImageFormat.Esd, Esd, "Windows 11 Pro"));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        _wimImages.Verify(w => w.KeepEditionAsWimAsync(Work, "Windows 11 Pro", It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Update.Should().Be(ServicedItemOutcome.Added);
    }

    [Fact]
    public async Task ServiceAsync_ExportFails_FallsBackWithoutMounting()
    {
        StageUpdate(Target);
        _files.Setup(f => f.FileExists(Esd)).Returns(true);
        _wimImages
            .Setup(w => w.KeepEditionAsWimAsync(Work, It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditionRemovalResult.Failed);
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ReturnsAsync(Image(ImageFormat.Esd, Esd, "Windows 11 Pro"));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.FellBackToSetup);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.PrepareImage, ServicingPhaseState.Failed));
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_PrepareImageThrows_FallsBackAndKeepsTheStepFailed()
    {
        StageUpdate(Target);
        _files.Setup(f => f.FileExists(Esd)).Returns(true);
        _wimImages
            .Setup(w => w.KeepEditionAsWimAsync(Work, It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk error"));
        _wimImages.Setup(w => w.DetectImageFormatAsync(Work)).ReturnsAsync(Image(ImageFormat.Esd, Esd, "Windows 11 Pro"));

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.FellBackToSetup);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.PrepareImage, ServicingPhaseState.Failed));
        _calls.Should().BeEmpty();
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_ResetBase_OnlyWhenTicked()
    {
        StageUpdate(Target);
        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);
        Args.Should().Contain($"/Image:\"{MountDir}\" /Cleanup-Image /StartComponentCleanup");
        Args.Should().NotContain(a => a.Contains("/ResetBase"));

        _calls.Clear();
        await Sut().ServiceAsync(Req() with { Cleanup = new ImageCleanupOptions(ResetBase: true) }, null, null, CancellationToken.None);

        Args.Should().Contain($"/Image:\"{MountDir}\" /Cleanup-Image /StartComponentCleanup /ResetBase");
    }

    [Fact]
    public async Task ServiceAsync_DotNet_RunsAfterCleanupAndBeforeTheCommit()
    {
        _files.Setup(f => f.DirectoryExists(Sxs)).Returns(true);

        await Sut().ServiceAsync(Req() with { EnableDotNet35 = true }, null, null, CancellationToken.None);

        Args.Should().SatisfyRespectively(
            a => a.Should().Be("/Cleanup-Mountpoints"),
            a => a.Should().StartWith("/Mount-Image"),
            a => a.Should().EndWith("/Cleanup-Image /StartComponentCleanup"),
            a => a.Should().Be($"/Image:\"{MountDir}\" /Enable-Feature /FeatureName:NetFx3 /All /LimitAccess /Source:\"{Sxs}\""),
            a => a.Should().EndWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_DotNetWithNoSxsFolder_IsSkipped()
    {
        StageUpdate(Target);

        var result = await Sut().ServiceAsync(Req() with { EnableDotNet35 = true }, null, null, CancellationToken.None);

        Args.Should().NotContain(a => a.Contains("NetFx3"));
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.DotNet, ServicingPhaseState.Skipped));
    }

    [Fact]
    public async Task ServiceAsync_Commit_IsFollowedByAnExportOfTheImage()
    {
        StageUpdate(Target);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        _events.FindIndex(e => e.EndsWith("/Commit")).Should().BeLessThan(_events.IndexOf("export"));
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.Export, ServicingPhaseState.Done));
    }

    [Fact]
    public async Task ServiceAsync_DiscardedBuild_IsNotExported()
    {
        StageUpdate(Target);
        _exit = a => a.Contains("/Add-Package") ? PackageNotApplicable : 0;

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        _wimImages.Verify(w => w.RewriteAsync(It.IsAny<string>(), It.IsAny<ImageFormat>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ServiceAsync_EsdOutput_ExportsInThatFormat()
    {
        StageUpdate(Target);

        await Sut().ServiceAsync(Req() with { OutputFormat = ImageFormat.Esd }, null, null, CancellationToken.None);

        _wimImages.Verify(w => w.RewriteAsync(Work, ImageFormat.Esd, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_ExportFails_KeepsTheCommittedImage()
    {
        StageUpdate(Target);
        _wimImages
            .Setup(w => w.RewriteAsync(Work, It.IsAny<ImageFormat>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditionRemovalResult.Failed);

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.Added);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.Export, ServicingPhaseState.Failed));
        _updateStep.Verify(u => u.RemoveAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_RemoveDotNetSource_DeletesTheFolderOnlyWhenAsked()
    {
        _files.Setup(f => f.DirectoryExists(Sxs)).Returns(true);
        _files.Setup(f => f.GetFiles(Sxs, "*", SearchOption.AllDirectories)).Returns([Sxs + @".cab"]);

        await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);
        _files.Verify(f => f.DeleteDirectory(Sxs, It.IsAny<bool>()), Times.Never);

        var result = await Sut().ServiceAsync(Req() with { RemoveDotNetSource = true }, null, null, CancellationToken.None);

        _calls.Should().BeEmpty();
        _files.Verify(f => f.SetFileAttributes(Sxs + @".cab", FileAttributes.Normal), Times.Once);
        _files.Verify(f => f.DeleteDirectory(Sxs, true), Times.Once);
        result.Phases.Should().Equal(new ServicingPhaseUpdate(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Done));
    }

    [Fact]
    public async Task ServiceAsync_DotNetNotInTheImage_KeepsTheSxsFolder()
    {
        _files.Setup(f => f.DirectoryExists(Sxs)).Returns(true);
        _exit = a => a.Contains("NetFx3") ? 1 : 0;

        var result = await Sut().ServiceAsync(Req() with { EnableDotNet35 = true, RemoveDotNetSource = true }, null, null, CancellationToken.None);

        _files.Verify(f => f.DeleteDirectory(Sxs, It.IsAny<bool>()), Times.Never);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.DotNet, ServicingPhaseState.Failed));
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Skipped));
    }

    [Fact]
    public async Task ServiceAsync_AppsRemoved_RunsOnTheMount()
    {
        await Sut().ServiceAsync(Req() with { Selections = Apps() }, null, null, CancellationToken.None);

        _appRemover.Verify(r => r.RemoveAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_AppRemovalFails_MarksTheStepFailedAndTheBuildContinues()
    {
        StageUpdate(Target);
        _appRemover
            .Setup(r => r.RemoveAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineStepOutcome(false, ["app"], 0));

        var result = await Sut().ServiceAsync(Req() with { Selections = Apps() }, null, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.Added);
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.RemoveApps, ServicingPhaseState.Failed));
        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_AppRemovalThrows_IsTreatedAsFailed()
    {
        StageUpdate(Target);
        _appRemover
            .Setup(r => r.RemoveAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("hive is locked"));

        var result = await Sut().ServiceAsync(Req() with { Selections = Apps() }, null, null, CancellationToken.None);

        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_SettingsFail_AreReportedAndTheBuildContinues()
    {
        StageUpdate(Target);
        _settings
            .Setup(r => r.ApplyAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineStepOutcome(false, ["setting"], 0));

        var result = await Sut().ServiceAsync(Req() with { Selections = Tweaks() }, null, null, CancellationToken.None);

        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.ApplySettings, ServicingPhaseState.Failed));
        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_SettingsOnlyAndOneFails_CommitsTheOnesThatApplied()
    {
        _settings
            .Setup(r => r.ApplyAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineStepOutcome(false, ["setting"], 2));

        var result = await Sut().ServiceAsync(Req() with { Selections = Tweaks() }, null, null, CancellationToken.None);

        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.ApplySettings, ServicingPhaseState.Failed));
        Args.Should().Contain(a => a.EndsWith("/Commit"));
        Args.Should().NotContain(a => a.EndsWith("/Discard"));
    }

    [Fact]
    public async Task ServiceAsync_SettingsOnlyAndNoneApply_DiscardsTheImage()
    {
        _settings
            .Setup(r => r.ApplyAsync(MountDir, It.IsAny<SelectionSet>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfflineStepOutcome(false, ["setting"], 0));

        await Sut().ServiceAsync(Req() with { Selections = Tweaks() }, null, null, CancellationToken.None);

        Args.Should().Contain(a => a.EndsWith("/Discard"));
        Args.Should().NotContain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_CleanupOptions_DeleteOnlyWhatIsTicked()
    {
        var download = MountDir + @"\Windows\SoftwareDistribution\Download";
        var temp = MountDir + @"\Windows\Temp";
        foreach (var folder in new[] { download, temp })
        {
            _files.Setup(f => f.DirectoryExists(folder)).Returns(true);
            _files.Setup(f => f.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)).Returns([folder + @"\a.tmp"]);
        }

        await Sut().ServiceAsync(Req() with { Cleanup = new ImageCleanupOptions(DownloadedUpdates: true) }, null, null, CancellationToken.None);

        _files.Verify(f => f.DeleteFile(download + @"\a.tmp"), Times.Once);
        _files.Verify(f => f.DeleteFile(temp + @"\a.tmp"), Times.Never);
    }

    [Fact]
    public async Task ServiceAsync_CleanupFileThatCannotBeDeleted_IsSkipped()
    {
        var logs = MountDir + @"\Windows\Logs\DISM";
        _files.Setup(f => f.DirectoryExists(logs)).Returns(true);
        _files.Setup(f => f.GetFiles(logs, "*", SearchOption.TopDirectoryOnly)).Returns([logs + @"\a.log", logs + @"\b.log"]);
        _files.Setup(f => f.DeleteFile(logs + @"\a.log")).Throws(new UnauthorizedAccessException());

        await Sut().ServiceAsync(Req() with { Cleanup = new ImageCleanupOptions(ServicingLogs: true) }, null, null, CancellationToken.None);

        _files.Verify(f => f.DeleteFile(logs + @"\b.log"), Times.Once);
        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_ReportsEachPhaseTransition()
    {
        StageUpdate(Target);
        var seen = new List<ServicingPhaseUpdate>();

        await Sut().ServiceAsync(Req(), null, new SyncProgress<ServicingPhaseUpdate>(seen.Add), CancellationToken.None);

        seen.Should().ContainInOrder(
            new ServicingPhaseUpdate(ServicingPhase.Mount, ServicingPhaseState.Running),
            new ServicingPhaseUpdate(ServicingPhase.Mount, ServicingPhaseState.Done),
            new ServicingPhaseUpdate(ServicingPhase.Update, ServicingPhaseState.Done),
            new ServicingPhaseUpdate(ServicingPhase.Save, ServicingPhaseState.Done),
            new ServicingPhaseUpdate(ServicingPhase.Export, ServicingPhaseState.Done));
    }

    [Fact]
    public async Task ServiceAsync_UpdateFails_OnlyThePhaseThatFailedIsFailed()
    {
        StageDrivers();
        StageUpdate(Target);
        _exit = a => a.Contains("/Add-Package") ? PackageNotApplicable : 0;

        var result = await Sut().ServiceAsync(Req(), null, null, CancellationToken.None);

        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.Update, ServicingPhaseState.Failed));
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.Drivers, ServicingPhaseState.Skipped));
        result.Phases.Should().Contain(new ServicingPhaseUpdate(ServicingPhase.Mount, ServicingPhaseState.Skipped));
    }

    [Fact]
    public void PhasesFor_NothingToDo_IsEmpty()
    {
        Sut().PhasesFor(Req()).Should().BeEmpty();
    }

    [Fact]
    public void PhasesFor_DriversOnly_ListsJustTheApplicablePhasesInOrder()
    {
        StageDrivers();

        Sut().PhasesFor(Req()).Should().Equal(
            ServicingPhase.Mount, ServicingPhase.Drivers, ServicingPhase.Cleanup, ServicingPhase.Save, ServicingPhase.Export);
    }

    [Fact]
    public void PhasesFor_EverythingRequested_ListsEveryPhaseInTheDeclaredOrder()
    {
        StageDrivers();
        StageUpdate(Target);
        _files.Setup(f => f.FileExists(Esd)).Returns(true);
        var request = Req() with
        {
            EnableDotNet35 = true,
            RemoveDotNetSource = true,
            Selections = new SelectionSet(Tweaks().Settings, Apps().WindowsApps, []),
        };

        var phases = Sut().PhasesFor(request);

        phases.Should().Equal(Enum.GetValues<ServicingPhase>());
    }

    [Fact]
    public void PhasesFor_DriversAndUpdateStillToStage_ListsTheirPhases()
    {
        Sut().PhasesFor(Req() with { StagesDrivers = true, StagesUpdate = true }).Should().Equal(
            ServicingPhase.Mount, ServicingPhase.Update, ServicingPhase.Drivers, ServicingPhase.Cleanup, ServicingPhase.Save, ServicingPhase.Export);
    }

    [Fact]
    public void PhasesFor_OnlyRemovingTheDotNetSource_NeedsNoMount()
    {
        Sut().PhasesFor(Req() with { RemoveDotNetSource = true }).Should().Equal(ServicingPhase.RemoveDotNetSource);
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    [Fact]
    public void MountDir_IsOutsideWorkingDirectory()
    {
        var mountDir = OfflineServicingService.MountDirFor(Work + "\\", _files.Object);

        mountDir.Should().Be(MountDir);
        mountDir.Should().NotStartWith(Work);
    }

    [Fact]
    public void MountDir_DriveRootWorkingDirectory_UsesTemp()
    {
        OfflineServicingService.MountDirFor(@"E:\", _files.Object).Should().Be(@"C:\Temp\WinhanceMount");
    }
}
