using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class BackgroundTaskServiceTests : IDisposable
{
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IScheduledTaskService> _tasks = new();
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IBloatRemovalService> _bloat = new();
    private readonly Mock<IInteractiveUserService> _user = new();
    private readonly Mock<IInstalledAppsService> _installed = new();
    private readonly string _lockRoot = Path.Combine(Path.GetTempPath(), "winhance-bts-" + Guid.NewGuid().ToString("N"));
    private readonly string _protectedExe = Path.Combine(Environment.SystemDirectory, "cmd.exe");
    private readonly string _writableFolder = Path.Combine(Path.GetTempPath(), "winhance-bts-exe-" + Guid.NewGuid().ToString("N"));
    private readonly BackgroundTaskService _service;

    public BackgroundTaskServiceTests()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(Array.Empty<WinhanceTaskInfo>());
        _registry.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Returns(true);
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));
        _user.Setup(u => u.InteractiveUserSid).Returns("S-1-5-21-1");
        _tasks.Setup(t => t.RegisterSignInTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(OperationResult.Succeeded());
        _tasks.Setup(t => t.UnregisterScheduledTaskAsync(It.IsAny<string>())).ReturnsAsync(OperationResult.Succeeded());
        _service = new BackgroundTaskService(
            _log.Object, _tasks.Object, _registry.Object,
            _installed.Object, _bloat.Object, _files.Object, _user.Object, _lockRoot, _protectedExe, isPortable: false);
    }

    public void Dispose()
    {
        if (Directory.Exists(_lockRoot))
            Directory.Delete(_lockRoot, recursive: true);
        if (Directory.Exists(_writableFolder))
            Directory.Delete(_writableFolder, recursive: true);
        GC.SuppressFinalize(this);
    }

    private BackgroundTaskService ServiceRunningFrom(string exePath, bool isPortable = false) => new(
        _log.Object, _tasks.Object, _registry.Object,
        _installed.Object, _bloat.Object, _files.Object, _user.Object, _lockRoot, exePath, isPortable);

    // A folder every user can write to, with a Winhance.exe in it.
    private string ExeInWritableFolder()
    {
        Directory.CreateDirectory(_writableFolder);
        var folder = new DirectoryInfo(_writableFolder);
        var security = folder.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        folder.SetAccessControl(security);
        var exe = Path.Combine(_writableFolder, "Winhance.exe");
        File.WriteAllText(exe, "x");
        return exe;
    }

    private void KeepOnWith(params string[] ids)
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.EnabledValue)).Returns(1);
        _registry.Setup(r => r.GetValueNames(BackgroundTaskService.KeptKey)).Returns(ids);
    }

    private void ScanFinds(string id, bool installed) =>
        _installed.Setup(i => i.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { [id] = installed }));

    private void RemovalReturns(RemovalOutcome outcome)
    {
        _bloat.Setup(b => b.ExecuteDedicatedScriptAsync(It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
        _bloat.Setup(b => b.ExecuteBloatRemovalAsync(It.IsAny<List<ItemDefinition>>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
    }

    private void VerifyResult(string result, Times times) =>
        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastResult", result, RegistryValueKind.String), times);

    [Fact]
    public async Task AddKeptAppsAsync_KnownId_WritesTodaysDate()
    {
        await _service.AddKeptAppsAsync(["windows-app-onedrive"]);

        _registry.Verify(r => r.SetValue(
            BackgroundTaskService.KeptKey, "windows-app-onedrive",
            DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task AddKeptAppsAsync_UnknownId_IsIgnoredAndLogged()
    {
        await _service.AddKeptAppsAsync(["calc.exe & del *"]);

        _registry.Verify(r => r.SetValue(It.IsAny<string>(), It.Is<string>(name => !KeepRemovedModeStore.IsSetting(name)), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("not in the app catalog")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AddKeptAppsAsync_AlreadyListed_KeepsTheOriginalDate()
    {
        _registry.Setup(r => r.ValueExists(BackgroundTaskService.KeptKey, "windows-app-onedrive")).Returns(true);

        await _service.AddKeptAppsAsync(["windows-app-onedrive"]);

        _registry.Verify(r => r.SetValue(It.IsAny<string>(), It.Is<string>(name => !KeepRemovedModeStore.IsSetting(name)), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public async Task AddKeptAppsAsync_RegistryRefusesTheWrite_Fails()
    {
        _registry.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Returns(false);

        var result = await _service.AddKeptAppsAsync(["windows-app-onedrive"]);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void RemoveKeptApp_DeletesTheEntryAndItsReturnCount()
    {
        _service.RemoveKeptApp("windows-app-onedrive");

        _registry.Verify(r => r.DeleteValue(BackgroundTaskService.KeptKey, "windows-app-onedrive"), Times.Once);
        _registry.Verify(r => r.DeleteValue(BackgroundTaskService.StatusKey, "windows-app-onedrive.ReturnCount"), Times.Once);
    }

    [Fact]
    public void GetKeptApps_ReadsNameDateAndReturnCount()
    {
        _registry.Setup(r => r.GetValueNames(BackgroundTaskService.KeptKey)).Returns(["windows-app-onedrive", "not-in-catalog"]);
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, "windows-app-onedrive")).Returns("2026-11-03");
        _registry.Setup(r => r.GetValue(BackgroundTaskService.StatusKey, "windows-app-onedrive.ReturnCount")).Returns(2);

        var apps = _service.GetKeptApps();

        apps.Should().HaveCount(2);
        var onedrive = apps.Single(a => a.Id == "windows-app-onedrive");
        onedrive.Name.Should().Be("OneDrive");
        onedrive.RemovedOn.Should().Be(new DateTime(2026, 11, 3));
        onedrive.ReturnCount.Should().Be(2);
        apps.Single(a => a.Id == "not-in-catalog").Name.Should().Be("not-in-catalog");
    }

    [Fact]
    public void GetStatus_SplitsTheRemovedAgainList()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.StatusKey, "LastResult")).Returns(KeepRemovedResult.RemovedAgain);
        _registry.Setup(r => r.GetValue(BackgroundTaskService.StatusKey, "RemovedAgain")).Returns("a;b");

        var status = _service.GetStatus();

        status.LastResult.Should().Be(KeepRemovedResult.RemovedAgain);
        status.RemovedAgain.Should().Equal("a", "b");
        status.LastCheck.Should().BeNull();
    }

    [Fact]
    public void GetStatus_ReadsWhenAnAppWasLastPutBack()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.StatusKey, "LastRemovedAgain")).Returns("2026-11-05T08:12:00.0000000+02:00");

        _service.GetStatus().LastRemovedAgain.Should().NotBeNull();
    }

    [Fact]
    public void WriteStatus_ARunThatRemovedNothing_KeepsTheLastRemoval()
    {
        _service.WriteStatus(KeepRemovedResult.AllStillRemoved, []);

        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastResult", KeepRemovedResult.AllStillRemoved, RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "RemovedAgain", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastRemovedAgain", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public void WriteStatus_ARunThatRemovedAnApp_RecordsItAndWhen()
    {
        var item = new Winhance.Core.Features.SoftwareApps.Models.ItemDefinition { Id = "windows-app-onedrive", Name = "OneDrive", Description = "" };

        _service.WriteStatus(KeepRemovedResult.RemovedAgain, [item]);

        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "RemovedAgain", "windows-app-onedrive", RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastRemovedAgain", It.IsAny<object>(), RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task GetOtherTasksAsync_LeavesOutTheSignInRemovalAndEdgeRedirectTasks()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(SignInTask.Name, "w.exe", "--tray", "User", true),
            new WinhanceTaskInfo(SignInTask.LegacyMaintainName, "w.exe", "--maintain", "SYSTEM", true),
            new WinhanceTaskInfo("EdgeRemoval", "powershell.exe", "-File x.ps1", "SYSTEM", true),
            new WinhanceTaskInfo(ScriptPaths.OpenWebSearchTaskName, "powershell.exe", "-File x.ps1", "SYSTEM", true),
            new WinhanceTaskInfo("LeftoverTest", "powershell.exe", "-File x.ps1", "SYSTEM", true),
        });

        var others = await _service.GetOtherTasksAsync();

        others.Should().ContainSingle().Which.Name.Should().Be("LeftoverTest");
    }

    [Fact]
    public async Task GetEdgeRedirectAsync_NoTaskAndNoFolder_IsNull()
    {
        (await _service.GetEdgeRedirectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetEdgeRedirectAsync_TaskAndFolder_IsActive()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(ScriptPaths.OpenWebSearchTaskName, "powershell.exe", "-File x.ps1", "SYSTEM", true),
        });
        _files.Setup(f => f.DirectoryExists(ScriptPaths.OpenWebSearchDirectoryLiteral)).Returns(true);

        var state = await _service.GetEdgeRedirectAsync();

        state!.Health.Should().Be(RemovalTaskHealth.Active);
        state.ScriptPath.Should().Be(ScriptPaths.OpenWebSearchDirectoryLiteral);
    }

    [Fact]
    public async Task GetEdgeRedirectAsync_TaskButNoFolder_IsScriptMissing()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(ScriptPaths.OpenWebSearchTaskName, "powershell.exe", "-File x.ps1", "SYSTEM", true),
        });

        (await _service.GetEdgeRedirectAsync())!.Health.Should().Be(RemovalTaskHealth.ScriptMissing);
    }

    [Fact]
    public async Task GetEdgeRedirectAsync_FolderButNoTask_IsNoTask()
    {
        _files.Setup(f => f.DirectoryExists(ScriptPaths.OpenWebSearchDirectoryLiteral)).Returns(true);

        (await _service.GetEdgeRedirectAsync())!.Health.Should().Be(RemovalTaskHealth.NoTask);
    }

    [Fact]
    public async Task GetEdgeRedirectAsync_DisabledTask_IsTaskDisabled()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(ScriptPaths.OpenWebSearchTaskName, "powershell.exe", "-File x.ps1", "SYSTEM", false),
        });
        _files.Setup(f => f.DirectoryExists(ScriptPaths.OpenWebSearchDirectoryLiteral)).Returns(true);

        (await _service.GetEdgeRedirectAsync())!.Health.Should().Be(RemovalTaskHealth.TaskDisabled);
    }

    [Fact]
    public async Task IsKeepRemovedOnAsync_ScriptsModeEmptyList_FollowsTheSavedSwitch()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.ModeValue)).Returns("Scripts");
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.EnabledValue)).Returns(1);

        (await _service.IsKeepRemovedOnAsync()).Should().BeTrue();
    }

    [Fact]
    public void GetKeptApps_GroupsEachAppUnderItsRemovalTask()
    {
        _registry.Setup(r => r.GetValueNames(BackgroundTaskService.KeptKey)).Returns(["windows-app-onedrive", "not-in-catalog"]);

        var apps = _service.GetKeptApps();

        apps.Single(a => a.Id == "windows-app-onedrive").Category.Should().Be("OneDriveRemoval");
        apps.Single(a => a.Id == "not-in-catalog").Category.Should().Be("BloatRemoval");
    }

    [Fact]
    public async Task GetRemovalTasksAsync_ScriptWithoutATask_IsNoTask()
    {
        _files.Setup(f => f.FileExists(It.Is<string>(path => path.EndsWith("EdgeRemoval.ps1")))).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("");

        var states = await _service.GetRemovalTasksAsync();

        states.Should().ContainSingle().Which.Health.Should().Be(RemovalTaskHealth.NoTask);
    }

    [Fact]
    public async Task GetRemovalTasksAsync_TaskWithoutItsScript_IsScriptMissing()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo("EdgeRemoval", "powershell.exe", "-Command ReadAllText('C:\\gone\\EdgeRemoval.ps1')", "SYSTEM", true),
        });

        var states = await _service.GetRemovalTasksAsync();

        states.Should().ContainSingle().Which.Health.Should().Be(RemovalTaskHealth.ScriptMissing);
    }

    [Fact]
    public async Task GetRemovalTasksAsync_DisabledTaskWithItsScript_IsTaskDisabled()
    {
        _files.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("");
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo("EdgeRemoval", "powershell.exe", "-File x.ps1", "SYSTEM", false),
        });

        var states = await _service.GetRemovalTasksAsync();

        states.Should().ContainSingle(s => s.Name == "EdgeRemoval").Which.Health.Should().Be(RemovalTaskHealth.TaskDisabled);
    }

    [Fact]
    public async Task GetRemovalTasksAsync_NeitherTaskNorScript_IsLeftOut()
    {
        (await _service.GetRemovalTasksAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SetModeAsync_AlreadyThatMode_DoesNothing()
    {
        var result = await _service.SetModeAsync(KeepRemovedMode.Winhance);

        result.Success.Should().BeTrue();
        _registry.Verify(r => r.SetValue(It.IsAny<string>(), KeepRemovedModeStore.ModeValue, It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public async Task SetModeAsync_ToScripts_WithKeepingOn_WritesScriptsAndStoresTheMode()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.EnabledValue)).Returns(1);
        _registry.Setup(r => r.GetValueNames(BackgroundTaskService.KeptKey)).Returns(["windows-app-onedrive"]);

        var result = await _service.SetModeAsync(KeepRemovedMode.Scripts);

        result.Success.Should().BeTrue();
        _bloat.Verify(b => b.WriteRemovalScriptsAsync(It.IsAny<List<Winhance.Core.Features.SoftwareApps.Models.ItemDefinition>>(), It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.SetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.ModeValue, "Scripts", RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task SetModeAsync_ToWinhance_DeletesTheTasksAndScripts()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.ModeValue)).Returns("Scripts");
        _files.Setup(f => f.FileExists(It.Is<string>(path => path.EndsWith("EdgeRemoval.ps1")))).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("");
        _tasks.Setup(t => t.IsTaskRegisteredAsync("EdgeRemoval")).ReturnsAsync(true);

        var result = await _service.SetModeAsync(KeepRemovedMode.Winhance);

        result.Success.Should().BeTrue();
        _tasks.Verify(t => t.UnregisterScheduledTaskAsync("EdgeRemoval"), Times.Once);
        _files.Verify(f => f.DeleteFile(It.Is<string>(path => path.EndsWith("EdgeRemoval.ps1"))), Times.Once);
        _registry.Verify(r => r.SetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.ModeValue, "Winhance", RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task SetSignInOptionAsync_TrayOn_RegistersTheOneTaskWithTheFlag()
    {
        var result = await _service.SetSignInOptionAsync(SignInTask.TrayFlag, true);

        result.Success.Should().BeTrue();
        _tasks.Verify(t => t.RegisterSignInTaskAsync(SignInTask.Name, It.IsAny<string>(), SignInTask.TrayFlag), Times.Once);
    }

    [Fact]
    public async Task SetSignInOptionAsync_ExeInProtectedFolder_RegistersTheExe()
    {
        await _service.SetSignInOptionAsync(SignInTask.TrayFlag, true);

        _tasks.Verify(t => t.RegisterSignInTaskAsync(SignInTask.Name, _protectedExe, SignInTask.TrayFlag), Times.Once);
    }

    [Fact]
    public async Task SetSignInOptionAsync_ExeFolderWritableByUsers_RefusesAndRegistersNothing()
    {
        var service = ServiceRunningFrom(ExeInWritableFolder());

        var result = await service.SetSignInOptionAsync(SignInTask.TrayFlag, true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(SignInTask.UnsafeFolderError);
        _tasks.Verify(t => t.RegisterSignInTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetSignInOptionAsync_PortableCopy_RefusesAndRegistersNothing()
    {
        var service = ServiceRunningFrom(_protectedExe, isPortable: true);

        var result = await service.SetSignInOptionAsync(SignInTask.ReapplyFlag, true);

        result.Success.Should().BeFalse();
        _tasks.Verify(t => t.RegisterSignInTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_RegisteredTaskPointsAtUnsafeExe_RemovesIt(bool portable)
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(SignInTask.Name, ExeInWritableFolder(), SignInTask.TrayFlag, "User", true),
        });
        _tasks.Setup(t => t.IsSignInTaskRegisteredForAsync(SignInTask.Name, "S-1-5-21-1")).ReturnsAsync(true);
        var service = ServiceRunningFrom(_protectedExe, isPortable: portable);

        await service.InitializeAsync();

        _tasks.Verify(t => t.UnregisterScheduledTaskAsync(SignInTask.Name), Times.Once);
        _tasks.Verify(t => t.RegisterSignInTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("Removing the sign-in task")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_PortableCopyWithTheInstalledCopysTask_LeavesItAlone()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(SignInTask.Name, _protectedExe, SignInTask.TrayFlag, "User", true),
        });
        _tasks.Setup(t => t.IsSignInTaskRegisteredForAsync(SignInTask.Name, "S-1-5-21-1")).ReturnsAsync(true);
        var service = ServiceRunningFrom(ExeInWritableFolder(), isPortable: true);

        await service.InitializeAsync();

        _tasks.Verify(t => t.UnregisterScheduledTaskAsync(SignInTask.Name), Times.Never);
        _tasks.Verify(t => t.RegisterSignInTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetSignInOptionAsync_TrayOn_WithKeepingOnInWinhanceMode_AddsMaintain()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.EnabledValue)).Returns(1);

        await _service.SetSignInOptionAsync(SignInTask.TrayFlag, true);

        _tasks.Verify(t => t.RegisterSignInTaskAsync(SignInTask.Name, It.IsAny<string>(), $"{SignInTask.TrayFlag} {SignInTask.MaintainFlag}"), Times.Once);
    }

    [Fact]
    public async Task SetSignInOptionAsync_LastOptionOff_RemovesTheTask()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(SignInTask.Name, "w.exe", SignInTask.TrayFlag, "User", true),
        });
        _tasks.Setup(t => t.IsSignInTaskRegisteredForAsync(SignInTask.Name, "S-1-5-21-1")).ReturnsAsync(true);

        await _service.SetSignInOptionAsync(SignInTask.TrayFlag, false);

        _tasks.Verify(t => t.UnregisterScheduledTaskAsync(SignInTask.Name), Times.Once);
        _tasks.Verify(t => t.RegisterSignInTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetSignInOptionsAsync_ReadsTheFlagsOfTheCombinedTask()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(SignInTask.Name, "w.exe", $"{SignInTask.ReapplyFlag} {SignInTask.MaintainFlag}", "User", true),
        });
        _tasks.Setup(t => t.IsSignInTaskRegisteredForAsync(SignInTask.Name, "S-1-5-21-1")).ReturnsAsync(true);

        var options = await _service.GetSignInOptionsAsync();

        options.Should().Be(new SignInOptions(false, true, true));
    }

    [Fact]
    public async Task GetSignInOptionsAsync_AnotherUsersTask_IsIgnored()
    {
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo(SignInTask.Name, "w.exe", SignInTask.TrayFlag, "Other", true),
        });

        (await _service.GetSignInOptionsAsync()).Should().Be(SignInOptions.None);
    }

    [Fact]
    public async Task InitializeAsync_DeletesTheOldSystemTask()
    {
        _tasks.Setup(t => t.IsTaskRegisteredAsync(SignInTask.LegacyMaintainName)).ReturnsAsync(true);

        await _service.InitializeAsync();

        _tasks.Verify(t => t.UnregisterScheduledTaskAsync(SignInTask.LegacyMaintainName), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_UpgradeWithRemovalScripts_ImportsTheirItemsIntoTheKeptList()
    {
        _files.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("");
        _tasks.Setup(t => t.GetWinhanceTasksAsync()).ReturnsAsync(new[]
        {
            new WinhanceTaskInfo("OneDriveRemoval", "powershell.exe", "-File x.ps1", "SYSTEM", true),
        });

        await _service.InitializeAsync();

        _registry.Verify(r => r.SetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.ModeValue, "Scripts", RegistryValueKind.String), Times.Once);
        _registry.Verify(r => r.SetValue(BackgroundTaskService.KeptKey, "windows-app-onedrive", It.IsAny<object>(), RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task MaintainAsync_ScriptsMode_DoesNothing()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.ModeValue)).Returns("Scripts");

        await _service.MaintainAsync();

        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastCheck", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public async Task MaintainAsync_NothingOnTheKeptList_ReportsNoApps()
    {
        KeepOnWith();

        await _service.MaintainAsync();

        VerifyResult(KeepRemovedResult.NoApps, Times.Once());
        _installed.Verify(i => i.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MaintainAsync_KeptAppStillGone_ReportsAllStillRemoved()
    {
        KeepOnWith("windows-app-onedrive");
        ScanFinds("windows-app-onedrive", installed: false);

        await _service.MaintainAsync();

        VerifyResult(KeepRemovedResult.AllStillRemoved, Times.Once());
        _bloat.Verify(b => b.ExecuteDedicatedScriptAsync(It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MaintainAsync_KeptAppCameBackAndRemovalWorks_ReportsRemovedAgain()
    {
        KeepOnWith("windows-app-onedrive");
        ScanFinds("windows-app-onedrive", installed: true);
        RemovalReturns(RemovalOutcome.Success);

        await _service.MaintainAsync();

        VerifyResult(KeepRemovedResult.RemovedAgain, Times.Once());
        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "RemovedAgain", "windows-app-onedrive", RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task MaintainAsync_KeptAppCameBackAndRemovalFails_ReportsRemovalFailed()
    {
        KeepOnWith("windows-app-onedrive");
        ScanFinds("windows-app-onedrive", installed: true);
        RemovalReturns(RemovalOutcome.Failed);

        await _service.MaintainAsync();

        VerifyResult(KeepRemovedResult.RemovalFailed, Times.Once());
        VerifyResult(KeepRemovedResult.RemovedAgain, Times.Never());
    }

    [Fact]
    public async Task MaintainAsync_AnotherCheckHoldsTheLock_DoesNotRunAgain()
    {
        KeepOnWith("windows-app-onedrive");
        ScanFinds("windows-app-onedrive", installed: true);
        RemovalReturns(RemovalOutcome.Success);
        Directory.CreateDirectory(_lockRoot);
        using var held = new FileStream(Path.Combine(_lockRoot, "keep-removed.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        await _service.MaintainAsync();

        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastCheck", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _installed.Verify(i => i.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()), Times.Never);
        _log.Verify(l => l.LogInformation(It.Is<string>(m => m.Contains("already running")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MaintainAsync_KeepingOff_DoesNothing()
    {
        _registry.Setup(r => r.GetValue(BackgroundTaskService.KeptKey, KeepRemovedModeStore.EnabledValue)).Returns(0);

        await _service.MaintainAsync();

        _registry.Verify(r => r.SetValue(BackgroundTaskService.StatusKey, "LastCheck", It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }
}
