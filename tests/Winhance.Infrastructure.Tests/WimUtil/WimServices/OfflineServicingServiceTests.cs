using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflineServicingServiceTests
{
    private const string Work = @"D:\iso\WinhanceWIM";
    private const string Wim = Work + @"\sources\install.wim";
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
    private readonly Mock<IDismImageInfoReader> _images = new();
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
        _files.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(1000L);
        _images.Setup(i => i.GetImageInfo(Wim)).Returns([new DismImageEntry(1, "Windows 11 Pro")]);
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _dism.Setup(r => r.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(true);
        _dism
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string args, IProgress<TaskProgressDetail>? _, CancellationToken token) =>
            {
                _calls.Add((args, token));
                return Task.FromResult((_exit(args), string.Empty));
            });
    }

    private OfflineServicingService Sut() => new(
        _dism.Object, _files.Object, _localization.Object, new Mock<ILogService>().Object,
        _driverStep.Object, _updateStep.Object, _stager.Object, _images.Object);

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

    private IEnumerable<string> Args => _calls.Select(c => c.Args);

    [Fact]
    public async Task ServiceAsync_DriversAndUpdate_RunsDismInOrder()
    {
        StageDrivers();
        StageUpdate(Checkpoint, Target);

        await Sut().ServiceAsync(Work, null, CancellationToken.None);

        Args.Should().SatisfyRespectively(
            a => a.Should().Be("/Cleanup-Mountpoints"),
            a => a.Should().Be($"/Mount-Image /ImageFile:\"{Wim}\" /Index:1 /MountDir:\"{MountDir}\""),
            a => a.Should().Be($"/Image:\"{MountDir}\" /Add-Driver /Driver:\"{Oem}\" /Recurse"),
            a => a.Should().StartWith($"/Image:\"{MountDir}\" /Add-Package /PackagePath:"),
            a => a.Should().Be($"/Image:\"{MountDir}\" /Cleanup-Image /StartComponentCleanup"),
            a => a.Should().Be($"/Unmount-Image /MountDir:\"{MountDir}\" /Commit"));
    }

    [Fact]
    public async Task ServiceAsync_ReadOnlyImage_ClearsTheFlagAfterCleanupAndBeforeTheMount()
    {
        StageDrivers();
        var dismCallsAtClear = -1;
        _files.Setup(f => f.SetFileAttributes(Wim, FileAttributes.Normal)).Callback(() => dismCallsAtClear = _calls.Count);

        await Sut().ServiceAsync(Work, null, CancellationToken.None);

        dismCallsAtClear.Should().Be(1);
        Args.ElementAt(1).Should().StartWith("/Mount-Image");
    }

    [Fact]
    public async Task ServiceAsync_UpdateTargetIsTheRecordedOneNotTheHighestKb()
    {
        StageUpdate(Checkpoint, Target, Older);

        await Sut().ServiceAsync(Work, null, CancellationToken.None);

        Args.Where(a => a.Contains("/Add-Package")).Should().ContainSingle()
            .Which.Should().EndWith($"/PackagePath:\"{UpdateDir}\\{Older}\"");
    }

    [Fact]
    public async Task ServiceAsync_UpdateFolderWithNoRecord_CountsAsNotStaged()
    {
        StageUpdate(Checkpoint, Target);
        _files.Setup(f => f.FileExists(TargetRecord)).Returns(false);

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.NotStaged));
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_RecordNamesAMissingFile_CountsAsNotStaged()
    {
        StageUpdate(Checkpoint, Target);
        _files.Setup(f => f.FileExists(UpdateDir + "\\" + Target)).Returns(false);

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Update.Should().Be(ServicedItemOutcome.NotStaged);
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_ImageHoldsMoreThanOneIndex_FallsBackWithoutMounting()
    {
        StageDrivers();
        StageUpdate(Target);
        _images.Setup(i => i.GetImageInfo(Wim)).Returns([new DismImageEntry(1, "Windows 11 Home"), new DismImageEntry(2, "Windows 11 Pro")]);

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup));
        _calls.Should().BeEmpty();
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_ImageCannotBeRead_FallsBackWithoutMounting()
    {
        StageUpdate(Target);
        _images.Setup(i => i.GetImageInfo(Wim)).Throws(new InvalidOperationException("The image is in use."));

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

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

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup));
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

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.Added));
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

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.Added));
        Args.Should().Contain(a => a.EndsWith("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_MountFails_AddsNothingAndEnsuresTheSteps()
    {
        StageDrivers();
        StageUpdate(Target);
        _exit = a => a.Contains("/Mount-Image") ? 1 : 0;

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

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

        var act = () => Sut().ServiceAsync(Work, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _calls.Last().Should().Be(($"/Unmount-Image /MountDir:\"{MountDir}\" /Discard", CancellationToken.None));
        Args.Should().NotContain(a => a.Contains("/Commit"));
    }

    [Fact]
    public async Task ServiceAsync_Commit_IgnoresTheCancellationToken()
    {
        StageUpdate(Target);
        using var cts = new CancellationTokenSource();

        await Sut().ServiceAsync(Work, null, cts.Token);

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

        var act = () => Sut().ServiceAsync(Work, null, cts.Token);

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

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup));
        _calls.Should().BeEmpty();
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_MountDirStillHoldsFiles_LeavesThemAndFallsBack()
    {
        StageUpdate(Target);
        _files.Setup(f => f.DirectoryExists(MountDir)).Returns(true);
        _files.Setup(f => f.GetFiles(MountDir, It.IsAny<string>(), It.IsAny<SearchOption>())).Returns([MountDir + @"\Windows"]);

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

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

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.Added, ServicedItemOutcome.Added));
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

        await Sut().ServiceAsync(Work, null, CancellationToken.None);

        order.Should().Equal("step", "writable", "delete");
    }

    [Fact]
    public async Task ServiceAsync_DriverStepCannotBeRemoved_KeepsTheDrivers()
    {
        StageDrivers();
        _driverStep.Setup(d => d.RemoveAsync(Work, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("autounattend.xml is in use"));

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

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

        await Sut().ServiceAsync(Work, null, CancellationToken.None);

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

        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup));
        _calls.Should().BeEmpty();
        _driverStep.Verify(d => d.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
        _updateStep.Verify(u => u.EnsureAsync(Work, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceAsync_NothingStaged_RunsNoDism()
    {
        var result = await Sut().ServiceAsync(Work, null, CancellationToken.None);

        result.Should().Be(new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.NotStaged));
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAsync_NoInstallWim_Throws()
    {
        _files.Setup(f => f.FileExists(Wim)).Returns(false);
        StageUpdate(Target);

        var act = () => Sut().ServiceAsync(Work, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("WIMUtil_Msg_ServicingNeedsWim");
        _calls.Should().BeEmpty();
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
