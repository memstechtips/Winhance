using System.IO;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep3DriversViewModelTests : IDisposable
{
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly WimStep3DriversViewModel _sut;

    public WimStep3DriversViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _sut = new WimStep3DriversViewModel(
            _mockWimCustomizationService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Constructor ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesAreDriversAddedToFalse()
    {
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesActionCards()
    {
        _sut.ExtractSystemDriversCard.Should().NotBeNull();
        _sut.SelectCustomDriversCard.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_BothActionCardsAreEnabled()
    {
        _sut.ExtractSystemDriversCard.IsEnabled.Should().BeTrue();
        _sut.SelectCustomDriversCard.IsEnabled.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    // ── ExtractAndAddSystemDrivers command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task ExtractAndAddSystemDriversCommand_WhenUserCancels_DoesNotExtract()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.AddDriversAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task ExtractAndAddSystemDriversCommand_OnSuccess_SetsAreDriversAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", null,
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeTrue();
        _sut.ExtractSystemDriversCard.IsComplete.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task ExtractAndAddSystemDriversCommand_OnFailure_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", null,
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.HasFailed.Should().BeTrue();
        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task ExtractAndAddSystemDriversCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Driver extraction error"));

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        _sut.ExtractSystemDriversCard.HasFailed.Should().BeTrue();
        _sut.ExtractSystemDriversCard.IsProcessing.Should().BeFalse();
        _sut.ExtractSystemDriversCard.IsEnabled.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task ExtractAndAddSystemDriversCommand_DisablesCardWhileProcessing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        bool wasProcessing = false;
        bool wasDisabled = false;

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                wasProcessing = _sut.ExtractSystemDriversCard.IsProcessing;
                wasDisabled = !_sut.ExtractSystemDriversCard.IsEnabled;
                return Task.FromResult(true);
            });

        await _sut.ExtractAndAddSystemDriversCommand.ExecuteAsync(null);

        wasProcessing.Should().BeTrue();
        wasDisabled.Should().BeTrue();
    }

    // ── SelectAndAddCustomDrivers command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_WhenCancelled_DoesNothing()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_WhenEmptyString_DoesNothing()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns(string.Empty);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_DirectoryDoesNotExist_SetsHasFailed()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(false);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_EmptyDirectory_SetsHasFailed()
    {
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        _mockFileSystemService
            .Setup(f => f.GetDirectories("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(Array.Empty<string>());

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_OnSuccess_SetsAreDriversAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(new[] { "C:\\DriverDir\\driver.inf" });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", "C:\\DriverDir",
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.AreDriversAdded.Should().BeTrue();
        _sut.SelectCustomDriversCard.IsComplete.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_OnFailure_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(new[] { "C:\\DriverDir\\driver.inf" });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                "C:\\WorkDir", "C:\\DriverDir",
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectAndAddCustomDriversCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFolder(It.IsAny<string?>()))
            .Returns("C:\\DriverDir");

        _mockFileSystemService
            .Setup(f => f.DirectoryExists("C:\\DriverDir"))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetFiles("C:\\DriverDir", "*", SearchOption.AllDirectories))
            .Returns(new[] { "C:\\DriverDir\\driver.inf" });

        _mockWimCustomizationService
            .Setup(s => s.AddDriversAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error adding drivers"));

        await _sut.SelectAndAddCustomDriversCommand.ExecuteAsync(null);

        _sut.SelectCustomDriversCard.HasFailed.Should().BeTrue();
        _sut.SelectCustomDriversCard.IsProcessing.Should().BeFalse();
        _sut.SelectCustomDriversCard.IsEnabled.Should().BeTrue();
    }

    // ── IDisposable ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep3DriversViewModel(
            _mockWimCustomizationService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    // ── Property change notifications ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SettingAreDriversAdded_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep3DriversViewModel.AreDriversAdded))
                raised = true;
        };

        _sut.AreDriversAdded = true;

        raised.Should().BeTrue();
    }
}
