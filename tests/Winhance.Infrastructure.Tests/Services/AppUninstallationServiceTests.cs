using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppUninstallationServiceTests
{
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IBloatRemovalService> _bloatRemovalService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IMultiScriptProgressService> _multiScriptProgressService = new();

    private AppUninstallationService CreateSut() => new(
        _logService.Object,
        _windowsAppsService.Object,
        _bloatRemovalService.Object,
        _taskProgressService.Object,
        _multiScriptProgressService.Object);

    // --- UninstallAppAsync ---

    [Fact]
    public async Task UninstallAppAsync_AppNotFound_ReturnsFailed()
    {
        var sut = CreateSut();

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync("nonexistent"))
            .ReturnsAsync((ItemDefinition?)null);

        var result = await sut.UninstallAppAsync("nonexistent");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("App not found");
    }

    [Fact]
    public async Task UninstallAppAsync_AppWithRemovalScript_RoutesDedicatedScript()
    {
        var sut = CreateSut();
        var app = new ItemDefinition
        {
            Id = "windows-app-edge",
            Name = "Microsoft Edge",
            Description = "Edge browser",
            RemovalScript = () => "echo removal"
        };

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync("windows-app-edge"))
            .ReturnsAsync(app);

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(app, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppAsync("windows-app-edge");

        result.Success.Should().BeTrue();
        _bloatRemovalService.Verify(
            x => x.ExecuteDedicatedScriptAsync(app, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _bloatRemovalService.Verify(
            x => x.ExecuteBloatRemovalAsync(It.IsAny<List<ItemDefinition>>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UninstallAppAsync_AppWithoutRemovalScript_RoutesBloatRemoval()
    {
        var sut = CreateSut();
        var app = new ItemDefinition
        {
            Id = "windows-app-calc",
            Name = "Calculator",
            Description = "Windows calculator",
            AppxPackageName = "Microsoft.WindowsCalculator"
        };

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync("windows-app-calc"))
            .ReturnsAsync(app);

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.Is<List<ItemDefinition>>(l => l.Count == 1 && l[0] == app),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppAsync("windows-app-calc");

        result.Success.Should().BeTrue();
        _bloatRemovalService.Verify(
            x => x.ExecuteBloatRemovalAsync(
                It.Is<List<ItemDefinition>>(l => l.Count == 1),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppAsync_DeferredOutcome_ReturnsDeferredSuccess()
    {
        var sut = CreateSut();
        var app = new ItemDefinition
        {
            Id = "windows-app-test",
            Name = "Test App",
            Description = "Test",
            AppxPackageName = "Microsoft.TestApp"
        };

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync("windows-app-test"))
            .ReturnsAsync(app);

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.DeferredToScheduledTask);

        var result = await sut.UninstallAppAsync("windows-app-test");

        result.Success.Should().BeTrue();
        result.InfoMessage.Should().Contain("next startup");
    }

    [Fact]
    public async Task UninstallAppAsync_FailedOutcome_ReturnsFailed()
    {
        var sut = CreateSut();
        var app = new ItemDefinition
        {
            Id = "windows-app-test",
            Name = "Test App",
            Description = "Test",
            AppxPackageName = "Microsoft.TestApp"
        };

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync("windows-app-test"))
            .ReturnsAsync(app);

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Failed);

        var result = await sut.UninstallAppAsync("windows-app-test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Removal failed");
    }

    [Fact]
    public async Task UninstallAppAsync_OperationCancelled_ReturnsCancelled()
    {
        var sut = CreateSut();

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.UninstallAppAsync("some-app");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task UninstallAppAsync_GenericException_ReturnsFailed()
    {
        var sut = CreateSut();

        _windowsAppsService
            .Setup(x => x.GetAppByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Something broke"));

        var result = await sut.UninstallAppAsync("some-app");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Something broke");
    }

    // --- UninstallAppsAsync ---

    [Fact]
    public async Task UninstallAppsAsync_EmptyList_ReturnsFailed()
    {
        var sut = CreateSut();

        var result = await sut.UninstallAppsAsync(new List<ItemDefinition>());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No apps provided");
    }

    [Fact]
    public async Task UninstallAppsAsync_NullList_ReturnsFailed()
    {
        var sut = CreateSut();

        var result = await sut.UninstallAppsAsync(null!);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No apps provided");
    }

    [Fact]
    public async Task UninstallAppsAsync_RegularApps_ExecutesBloatRemoval()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" },
            new() { Id = "app2", Name = "App2", Description = "Desc2", AppxPackageName = "Microsoft.App2" }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppsAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(2);
        _bloatRemovalService.Verify(
            x => x.ExecuteBloatRemovalAsync(
                It.Is<List<ItemDefinition>>(l => l.Count == 2),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppsAsync_ScriptApps_ExecutesDedicatedScripts()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "windows-app-edge",
                Name = "Edge",
                Description = "Edge",
                RemovalScript = () => "echo edge"
            }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppsAsync(apps);

        result.Success.Should().BeTrue();
        _bloatRemovalService.Verify(
            x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppsAsync_MixedApps_ExecutesBothPaths()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "windows-app-edge",
                Name = "Edge",
                Description = "Edge",
                RemovalScript = () => "echo edge"
            },
            new()
            {
                Id = "app1",
                Name = "App1",
                Description = "Desc1",
                AppxPackageName = "Microsoft.App1"
            }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppsAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(2);
        _bloatRemovalService.Verify(
            x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _bloatRemovalService.Verify(
            x => x.ExecuteBloatRemovalAsync(
                It.Is<List<ItemDefinition>>(l => l.Count == 1),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppsAsync_SaveRemovalScripts_PersistsScripts()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        await sut.UninstallAppsAsync(apps, saveRemovalScripts: true);

        _bloatRemovalService.Verify(x => x.PersistRemovalScriptsAsync(apps), Times.Once);
        _bloatRemovalService.Verify(x => x.CleanupAllRemovalArtifactsAsync(), Times.Never);
    }

    [Fact]
    public async Task UninstallAppsAsync_DontSaveRemovalScripts_CleansUp()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        await sut.UninstallAppsAsync(apps, saveRemovalScripts: false);

        _bloatRemovalService.Verify(x => x.CleanupAllRemovalArtifactsAsync(), Times.Once);
        _bloatRemovalService.Verify(x => x.PersistRemovalScriptsAsync(It.IsAny<List<ItemDefinition>>()), Times.Never);
    }

    [Fact]
    public async Task UninstallAppsAsync_DeferredOutcome_ReturnsDeferredSuccess()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "windows-app-edge",
                Name = "Edge",
                Description = "Edge",
                RemovalScript = () => "echo edge"
            }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.DeferredToScheduledTask);

        var result = await sut.UninstallAppsAsync(apps);

        result.Success.Should().BeTrue();
        result.InfoMessage.Should().Contain("next startup");
    }

    [Fact]
    public async Task UninstallAppsAsync_OperationCancelled_ReturnsCancelled()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" }
        };

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.UninstallAppsAsync(apps);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- UninstallAppsInParallelAsync ---

    [Fact]
    public async Task UninstallAppsInParallelAsync_EmptyList_ReturnsFailed()
    {
        var sut = CreateSut();

        var result = await sut.UninstallAppsInParallelAsync(new List<ItemDefinition>());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No apps provided");
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_NullList_ReturnsFailed()
    {
        var sut = CreateSut();

        var result = await sut.UninstallAppsInParallelAsync(null!);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No apps provided");
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_RegularApps_ExecutesBloatRemovalInParallel()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" },
            new() { Id = "app2", Name = "App2", Description = "Desc2", AppxPackageName = "Microsoft.App2" }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppsInParallelAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(2);
        _multiScriptProgressService.Verify(x => x.CompleteMultiScriptTask(), Times.Once);
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_ScriptApps_ExecutesDedicatedScriptsInParallel()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "windows-app-edge",
                Name = "Edge",
                Description = "Edge",
                RemovalScript = () => "echo edge"
            },
            new()
            {
                Id = "windows-app-onedrive",
                Name = "OneDrive",
                Description = "OneDrive",
                RemovalScript = () => "echo onedrive"
            }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppsInParallelAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(2);
        _multiScriptProgressService.Verify(
            x => x.StartMultiScriptTask(It.Is<string[]>(s => s.Length == 2)),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_MixedApps_HandlesScriptsAndRegularInParallel()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "windows-app-edge",
                Name = "Edge",
                Description = "Edge",
                RemovalScript = () => "echo edge"
            },
            new()
            {
                Id = "app1",
                Name = "App1",
                Description = "Desc1",
                AppxPackageName = "Microsoft.App1"
            }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        var result = await sut.UninstallAppsInParallelAsync(apps);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(2);
        // Slot names: "EdgeRemoval" + "BloatRemoval"
        _multiScriptProgressService.Verify(
            x => x.StartMultiScriptTask(It.Is<string[]>(s =>
                s.Length == 2 && s[0] == "EdgeRemoval" && s[1] == "BloatRemoval")),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_SaveRemovalScripts_PersistsScripts()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        await sut.UninstallAppsInParallelAsync(apps, saveRemovalScripts: true);

        _bloatRemovalService.Verify(x => x.PersistRemovalScriptsAsync(apps), Times.Once);
        _bloatRemovalService.Verify(x => x.CleanupAllRemovalArtifactsAsync(), Times.Never);
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_DontSaveRemovalScripts_CleansUp()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.Success);

        await sut.UninstallAppsInParallelAsync(apps, saveRemovalScripts: false);

        _bloatRemovalService.Verify(x => x.CleanupAllRemovalArtifactsAsync(), Times.Once);
        _bloatRemovalService.Verify(x => x.PersistRemovalScriptsAsync(It.IsAny<List<ItemDefinition>>()), Times.Never);
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_DeferredOutcome_ReturnsDeferredSuccess()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new()
            {
                Id = "windows-app-edge",
                Name = "Edge",
                Description = "Edge",
                RemovalScript = () => "echo edge"
            }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteDedicatedScriptAsync(
                It.IsAny<ItemDefinition>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RemovalOutcome.DeferredToScheduledTask);

        var result = await sut.UninstallAppsInParallelAsync(apps);

        result.Success.Should().BeTrue();
        result.InfoMessage.Should().Contain("next startup");
    }

    [Fact]
    public async Task UninstallAppsInParallelAsync_AlwaysCallsCompleteMultiScriptTask()
    {
        var sut = CreateSut();
        var apps = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1", AppxPackageName = "Microsoft.App1" }
        };

        var cts = new CancellationTokenSource();
        _multiScriptProgressService
            .Setup(x => x.StartMultiScriptTask(It.IsAny<string[]>()))
            .Returns(cts);

        _multiScriptProgressService
            .Setup(x => x.CreateScriptProgress(It.IsAny<int>()))
            .Returns(new Progress<TaskProgressDetail>());

        _bloatRemovalService
            .Setup(x => x.ExecuteBloatRemovalAsync(
                It.IsAny<List<ItemDefinition>>(),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var result = await sut.UninstallAppsInParallelAsync(apps);

        result.Success.Should().BeFalse();
        // Verify cleanup still called even when exception occurs (finally block)
        _multiScriptProgressService.Verify(x => x.CompleteMultiScriptTask(), Times.Once);
    }
}
