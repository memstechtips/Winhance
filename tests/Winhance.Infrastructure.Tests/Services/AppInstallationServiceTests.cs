using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppInstallationServiceTests
{
    private readonly Mock<ILegacyCapabilityService> _capabilityService = new();
    private readonly Mock<IOptionalFeatureService> _featureService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<IBloatRemovalService> _bloatRemovalService = new();
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();

    private AppInstallationService CreateSut() => new(
        _capabilityService.Object,
        _featureService.Object,
        _logService.Object,
        _windowsAppsService.Object,
        _externalAppsService.Object,
        _bloatRemovalService.Object,
        _scheduledTaskService.Object,
        _taskProgressService.Object,
        _fileSystemService.Object);

    // --- InstallAppAsync: routes to WindowsAppsService ---

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
            AppxPackageName = "Microsoft.WinApp"
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

    // --- InstallAppAsync: routes to ExternalAppsService ---

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

    // --- InstallAppAsync: routes to capability service ---

    [Fact]
    public async Task InstallAppAsync_CapabilityApp_RoutesToCapabilityService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "cap-app",
            Name = "Test Capability",
            Description = "A capability app",
            CapabilityName = "App.StepsRecorder"
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _capabilityService
            .Setup(x => x.EnableCapabilityAsync("App.StepsRecorder", "Test Capability", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _capabilityService.Verify(
            x => x.EnableCapabilityAsync("App.StepsRecorder", "Test Capability", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_CapabilityFails_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "cap-app",
            Name = "Test Capability",
            Description = "A capability app",
            CapabilityName = "App.StepsRecorder"
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _capabilityService
            .Setup(x => x.EnableCapabilityAsync("App.StepsRecorder", "Test Capability", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to launch PowerShell for capability");
    }

    // --- InstallAppAsync: routes to optional feature service ---

    [Fact]
    public async Task InstallAppAsync_OptionalFeatureApp_RoutesToFeatureService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "feature-app",
            Name = "Test Feature",
            Description = "An optional feature",
            OptionalFeatureName = "TelnetClient"
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _featureService
            .Setup(x => x.EnableFeatureAsync("TelnetClient", "Test Feature", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

    // --- InstallAppAsync: unsupported type ---

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

    // --- InstallAppAsync: bloat script cleanup ---

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

    // --- InstallAppAsync: Edge cleanup ---

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
            AppxPackageName = "Microsoft.MicrosoftEdge"
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
        // Also cleans up OpenWebSearch
        _scheduledTaskService.Verify(x => x.UnregisterScheduledTaskAsync("OpenWebSearchRepair"), Times.Once);
    }

    // --- InstallAppsAsync: batch install ---

    [Fact]
    public async Task InstallAppsAsync_MultipleApps_ReturnsSuccessCount()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "app1",
                Name = "App1",
                Description = "Desc1",
                WinGetPackageId = new[] { "Publisher.App1" }
            },
            new()
            {
                Id = "app2",
                Name = "App2",
                Description = "Desc2",
                WinGetPackageId = new[] { "Publisher.App2" }
            }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        _externalAppsService
            .Setup(x => x.InstallAppAsync(It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var result = await sut.InstallAppsAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(2);
    }

    [Fact]
    public async Task InstallAppsAsync_PartialSuccess_ReturnsPartialCount()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "app1",
                Name = "App1",
                Description = "Desc1",
                WinGetPackageId = new[] { "Publisher.App1" }
            },
            new()
            {
                Id = "app2",
                Name = "App2",
                Description = "Desc2",
                WinGetPackageId = new[] { "Publisher.App2" }
            }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ReturnsAsync(true);

        // First succeeds, second fails
        _externalAppsService
            .SetupSequence(x => x.InstallAppAsync(It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true))
            .ReturnsAsync(OperationResult<bool>.Failed("Failed"));

        var result = await sut.InstallAppsAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(1);
    }

    [Fact]
    public async Task InstallAppsAsync_EmptyList_ReturnsFailed()
    {
        var sut = CreateSut();

        var result = await sut.InstallAppsAsync(new List<ItemDefinition>());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No apps provided");
    }

    [Fact]
    public async Task InstallAppsAsync_NullList_ReturnsFailed()
    {
        var sut = CreateSut();

        var result = await sut.InstallAppsAsync(null!);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No apps provided");
    }

    [Fact]
    public async Task InstallAppsAsync_OperationCancelled_ReturnsCancelled()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "app1",
                Name = "App1",
                Description = "Desc1",
                WinGetPackageId = new[] { "Publisher.App1" }
            }
        };

        _bloatRemovalService
            .Setup(x => x.RemoveItemsFromScriptAsync(It.IsAny<List<ItemDefinition>>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.InstallAppsAsync(apps);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- InstallAppAsync: RequiresDirectDownload routes to external ---

    [Fact]
    public async Task InstallAppAsync_RequiresDirectDownload_RoutesToExternalAppsService()
    {
        var sut = CreateSut();
        var customProps = new Dictionary<string, object> { { "RequiresDirectDownload", true } };
        var item = new ItemDefinition
        {
            Id = "direct-app",
            Name = "Direct App",
            Description = "Needs direct download",
            CustomProperties = customProps
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
}
