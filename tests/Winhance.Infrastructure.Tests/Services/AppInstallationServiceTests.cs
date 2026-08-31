using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppInstallationServiceTests
{
    private static readonly string[] App1PackageId = ["Publisher.App1"];

    private readonly Mock<ILegacyCapabilityService> _capabilityService = new();
    private readonly Mock<IOptionalFeatureService> _featureService = new();
    private readonly Mock<IServicingSession> _servicingSession = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<IBloatRemovalService> _bloatRemovalService = new();
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IChangeHistoryService> _changeHistoryService = new();
    private IReadOnlyList<string>? _statements;
    private string? _label;

    private AppInstallationService CreateSut() => new(
        _capabilityService.Object,
        _featureService.Object,
        _servicingSession.Object,
        _logService.Object,
        _windowsAppsService.Object,
        _externalAppsService.Object,
        _bloatRemovalService.Object,
        _scheduledTaskService.Object,
        _taskProgressService.Object,
        _fileSystemService.Object,
        _changeHistoryService.Object);

    [Fact]
    public async Task InstallAppAsync_WindowsStoreApp_RoutesToWindowsAppsService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "windows-app-test",
            Name = "Test Windows App",
            Description = "A windows store app",
            WinGetPackageId = new[] { "Publisher.WinApp" },
            AppxPackageName = new[] { "Microsoft.WinApp" }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _windowsAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _windowsAppsService.Verify(
            x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Once);
        _externalAppsService.Verify(
            x => x.InstallAppAsync(It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_ExternalApp_RoutesToExternalAppsService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app-test",
            Name = "Test External App",
            Description = "An external app without AppxPackageName",
            WinGetPackageId = new[] { "Publisher.ExtApp" }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _externalAppsService.Verify(
            x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Once);
        _windowsAppsService.Verify(
            x => x.InstallAppAsync(It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_UnsupportedApp_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "unsupported-app",
            Name = "Unsupported App",
            Description = "No install info"
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public async Task InstallAppAsync_ShouldRemoveFromBloatScript_CallsRemoveItemsFromScript()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "Test App",
            Description = "Test",
            WinGetPackageId = new[] { "Publisher.App" }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.InstallAppAsync(item, shouldRemoveFromBloatScript: true);

        _bloatRemovalService.Verify(
            x => x.RemoveItemsFromScriptAsync(It.Is<List<ItemDefinition>>(l => l.Count == 1 && l[0] == item)),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ShouldNotRemoveFromBloatScript_SkipsBloatRemoval()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "Test App",
            Description = "Test",
            WinGetPackageId = new[] { "Publisher.App" }
        };

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.InstallAppAsync(item, shouldRemoveFromBloatScript: false);

        _bloatRemovalService.Verify(
            x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_EdgeApp_CleansUpDedicatedArtifacts()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "windows-app-edge",
            Name = "Microsoft Edge",
            Description = "Edge browser",
            WinGetPackageId = new[] { "Microsoft.Edge" },
            AppxPackageName = new[] { "Microsoft.MicrosoftEdge" }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _fileSystemService
            .Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns(true);

        _fileSystemService
            .Setup(x => x.CombinePath(It.IsAny<string[]>()))
            .Returns("C:\\test\\EdgeRemoval.ps1");

        _fileSystemService
            .Setup(x => x.DirectoryExists(It.IsAny<string>()))
            .Returns(true);

        _scheduledTaskService
            .Setup(x => x.UnregisterScheduledTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _windowsAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.InstallAppAsync(item);

        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.AtLeastOnce);
        _scheduledTaskService.Verify(x => x.UnregisterScheduledTaskAsync("EdgeRemoval"), Times.Once);
        _scheduledTaskService.Verify(x => x.UnregisterScheduledTaskAsync("OpenWebSearchRepair"), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_DownloadUrlOnly_RoutesToExternalAppsService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "download-only-app",
            Name = "Download Only App",
            Description = "Has only a download URL, no WinGet or Store ID",
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/installer.exe"
            }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _externalAppsService.Verify(
            x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_RequiresDirectDownload_RoutesToExternalAppsService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "direct-app",
            Name = "Direct App",
            Description = "Needs direct download",
            ExternalApp = new ExternalAppMetadata { RequiresDirectDownload = true }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _externalAppsService.Verify(
            x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_Success_LogsAppInstalled()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "Test External App",
            Description = "An external app",
            WinGetPackageId = new[] { "Publisher.ExtApp" }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.InstallAppAsync(item);

        _changeHistoryService.Verify(
            x => x.LogAppChange("Test External App", AppChangeKind.Installed), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_Failure_DoesNotLogAppInstalled()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "Test External App",
            Description = "An external app",
            WinGetPackageId = new[] { "Publisher.ExtApp" }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Failed("Install failed"));

        await sut.InstallAppAsync(item);

        _changeHistoryService.Verify(
            x => x.LogAppChange(It.IsAny<string>(), It.IsAny<AppChangeKind>()), Times.Never);
    }

    private void ArrangeServicingSession(bool launched = true)
    {
        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);
        _featureService
            .Setup(x => x.BuildEnableStatement(It.IsAny<IReadOnlyList<string>>()))
            .Returns("FEATURES");
        _capabilityService
            .Setup(x => x.BuildEnableStatement(It.IsAny<IReadOnlyList<string>>()))
            .Returns("CAPABILITIES");
        _servicingSession
            .Setup(x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, IProgress<TaskProgressDetail>?, CancellationToken>(
                (statements, label, _, _) => { _statements = statements; _label = label; })
            .ReturnsAsync(launched);
    }

    private void VerifySessionsRun(Times times) => _servicingSession.Verify(
        x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
        times);

    // The bug this replaced: features and capabilities were dispatched as two batches, so two CBS
    // servicing sessions were live at once and the second one failed.
    [Fact]
    public async Task EnableServicingBatchAsync_BothKinds_RunsOneSessionCarryingBothStatements()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "f1", Name = "Sandbox", Description = "d", OptionalFeatureName = "Containers-DisposableClientVM" },
            new() { Id = "c1", Name = "OpenSSH Client", Description = "d", CapabilityName = "OpenSSH.Client" }
        };
        ArrangeServicingSession();

        var result = await sut.EnableServicingBatchAsync(apps);

        result.Success.Should().BeTrue();
        VerifySessionsRun(Times.Once());
        _statements.Should().Equal("FEATURES", "CAPABILITIES");
        _label.Should().Be("Sandbox, OpenSSH Client");
    }

    [Fact]
    public async Task EnableServicingBatchAsync_BothKinds_LogsEnableStartedOncePerApp()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "f1", Name = "Sandbox", Description = "d", OptionalFeatureName = "Containers-DisposableClientVM" },
            new() { Id = "c1", Name = "OpenSSH Client", Description = "d", CapabilityName = "OpenSSH.Client" }
        };
        ArrangeServicingSession();

        await sut.EnableServicingBatchAsync(apps);

        _changeHistoryService.Verify(x => x.LogAppChange("Sandbox", AppChangeKind.EnableStarted), Times.Once);
        _changeHistoryService.Verify(x => x.LogAppChange("OpenSSH Client", AppChangeKind.EnableStarted), Times.Once);
        _changeHistoryService.Verify(x => x.LogAppChange(It.IsAny<string>(), It.IsAny<AppChangeKind>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EnableServicingBatchAsync_ThreeFeatures_SendsAllThreeNamesInOneStatement()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "f1", Name = "WSL", Description = "d", OptionalFeatureName = "Microsoft-Windows-Subsystem-Linux" },
            new() { Id = "f2", Name = ".NET 3.5", Description = "d", OptionalFeatureName = "NetFx3" },
            new() { Id = "f3", Name = "Sandbox", Description = "d", OptionalFeatureName = "Containers-DisposableClientVM" }
        };
        ArrangeServicingSession();

        var result = await sut.EnableServicingBatchAsync(apps);

        result.Success.Should().BeTrue();
        VerifySessionsRun(Times.Once());
        _statements.Should().Equal("FEATURES");
        _featureService.Verify(
            x => x.BuildEnableStatement(It.Is<IReadOnlyList<string>>(n => n.Count == 3
                && n[0] == "Microsoft-Windows-Subsystem-Linux"
                && n[1] == "NetFx3"
                && n[2] == "Containers-DisposableClientVM")),
            Times.Once);
    }

    [Fact]
    public async Task EnableServicingBatchAsync_ThreeCapabilities_SendsAllThreeNamesInOneStatement()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "c1", Name = "OpenSSH Client", Description = "d", CapabilityName = "OpenSSH.Client" },
            new() { Id = "c2", Name = "OpenSSH Server", Description = "d", CapabilityName = "OpenSSH.Server" },
            new() { Id = "c3", Name = "Steps Recorder", Description = "d", CapabilityName = "App.StepsRecorder" }
        };
        ArrangeServicingSession();

        var result = await sut.EnableServicingBatchAsync(apps);

        result.Success.Should().BeTrue();
        VerifySessionsRun(Times.Once());
        _statements.Should().Equal("CAPABILITIES");
        _capabilityService.Verify(
            x => x.BuildEnableStatement(It.Is<IReadOnlyList<string>>(n => n.Count == 3
                && n[0] == "OpenSSH.Client"
                && n[1] == "OpenSSH.Server"
                && n[2] == "App.StepsRecorder")),
            Times.Once);
    }

    [Fact]
    public async Task EnableServicingBatchAsync_AppCarryingBothNames_IsEnabledOnlyAsACapability()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "both",
                Name = "Two Names",
                Description = "d",
                CapabilityName = "OpenSSH.Client",
                OptionalFeatureName = "NetFx3"
            }
        };
        ArrangeServicingSession();

        await sut.EnableServicingBatchAsync(apps);

        _statements.Should().Equal("CAPABILITIES");
        _featureService.Verify(x => x.BuildEnableStatement(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        _changeHistoryService.Verify(x => x.LogAppChange("Two Names", AppChangeKind.EnableStarted), Times.Once);
    }

    [Fact]
    public async Task EnableServicingBatchAsync_Launched_DefersAndNeverLogsAnInstall()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "f3", Name = "Sandbox", Description = "d", OptionalFeatureName = "Containers-DisposableClientVM" }
        };
        ArrangeServicingSession();

        var result = await sut.EnableServicingBatchAsync(apps);

        result.Success.Should().BeTrue();
        result.InfoMessage.Should().NotBeNullOrEmpty();
        _changeHistoryService.Verify(
            x => x.LogAppChange("Sandbox", AppChangeKind.EnableStarted), Times.Once);
        _changeHistoryService.Verify(
            x => x.LogAppChange(It.IsAny<string>(), AppChangeKind.Installed), Times.Never);
    }

    [Fact]
    public async Task EnableServicingBatchAsync_LaunchFails_ReturnsFailedAndWritesNoHistory()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "c1", Name = "OpenSSH Client", Description = "d", CapabilityName = "OpenSSH.Client" }
        };
        ArrangeServicingSession(launched: false);

        var result = await sut.EnableServicingBatchAsync(apps);

        result.Success.Should().BeFalse();
        _changeHistoryService.Verify(
            x => x.LogAppChange(It.IsAny<string>(), It.IsAny<AppChangeKind>()), Times.Never);
    }

    [Fact]
    public async Task EnableServicingBatchAsync_NothingToService_ReturnsFailedAndStartsNoSession()
    {
        var sut = CreateSut();
        ArrangeServicingSession();

        var result = await sut.EnableServicingBatchAsync(new List<ItemDefinition>
        {
            new() { Id = "a1", Name = "Plain App", Description = "d", WinGetPackageId = App1PackageId }
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No apps provided");
        VerifySessionsRun(Times.Never());
    }
}
