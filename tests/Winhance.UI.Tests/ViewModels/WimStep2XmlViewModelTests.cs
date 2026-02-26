using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep2XmlViewModelTests : IDisposable
{
    private readonly Mock<IAutounattendXmlGeneratorService> _mockXmlGeneratorService = new();
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly WimStep2XmlViewModel _sut;

    public WimStep2XmlViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        _sut = new WimStep2XmlViewModel(
            _mockXmlGeneratorService.Object,
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
    public void Constructor_InitializesSelectedXmlPathToEmpty()
    {
        _sut.SelectedXmlPath.Should().BeEmpty();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesXmlStatusFromLocalization()
    {
        _sut.XmlStatus.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesIsXmlAddedToFalse()
    {
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_InitializesActionCards()
    {
        _sut.GenerateWinhanceXmlCard.Should().NotBeNull();
        _sut.DownloadXmlCard.Should().NotBeNull();
        _sut.SelectXmlCard.Should().NotBeNull();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_AllActionCardsAreEnabled()
    {
        _sut.GenerateWinhanceXmlCard.IsEnabled.Should().BeTrue();
        _sut.DownloadXmlCard.IsEnabled.Should().BeTrue();
        _sut.SelectXmlCard.IsEnabled.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    // ── GenerateWinhanceXml command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task GenerateWinhanceXmlCommand_WhenUserCancels_DoesNotGenerate()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockXmlGeneratorService.Verify(s => s.GenerateFromCurrentSelectionsAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<ConfigurationItem>?>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_SetsIsXmlAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockXmlGeneratorService
            .Setup(s => s.GenerateFromCurrentSelectionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConfigurationItem>?>()))
            .ReturnsAsync("C:\\WorkDir\\autounattend.xml");

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.SelectedXmlPath.Should().Be("C:\\WorkDir\\autounattend.xml");
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_ClearsOtherCardCompletions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockXmlGeneratorService
            .Setup(s => s.GenerateFromCurrentSelectionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConfigurationItem>?>()))
            .ReturnsAsync("C:\\WorkDir\\autounattend.xml");

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task GenerateWinhanceXmlCommand_OnException_SetsHasFailed()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockXmlGeneratorService
            .Setup(s => s.GenerateFromCurrentSelectionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConfigurationItem>?>()))
            .ThrowsAsync(new Exception("Generation failed"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.HasFailed.Should().BeTrue();
    }

    // ── DownloadUnattendedWinstallXml command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task DownloadUnattendedWinstallXmlCommand_OnSuccess_SetsIsXmlAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("downloaded content");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.DownloadXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task DownloadUnattendedWinstallXmlCommand_WhenAddFails_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("downloaded content");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.HasFailed.Should().BeTrue();
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task DownloadUnattendedWinstallXmlCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Download failed"));

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.HasFailed.Should().BeTrue();
    }

    // ── SelectXmlFile command ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectXmlFileCommand_WhenCancelled_DoesNothing()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public async Task SelectXmlFileCommand_WhenEmptyString_DoesNothing()
    {
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
    }

    // ── ClearOtherXmlCardCompletions ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void ClearOtherXmlCardCompletions_ExceptGenerate_ClearsDownloadAndSelect()
    {
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _sut.ClearOtherXmlCardCompletions("generate");

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void ClearOtherXmlCardCompletions_ExceptDownload_ClearsGenerateAndSelect()
    {
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _sut.ClearOtherXmlCardCompletions("download");

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
        _sut.DownloadXmlCard.IsComplete.Should().BeTrue();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void ClearOtherXmlCardCompletions_ExceptSelect_ClearsGenerateAndDownload()
    {
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _sut.ClearOtherXmlCardCompletions("select");

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeTrue();
    }

    // ── IDisposable ──

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep2XmlViewModel(
            _mockXmlGeneratorService.Object,
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
    public void SettingIsXmlAdded_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep2XmlViewModel.IsXmlAdded))
                raised = true;
        };

        _sut.IsXmlAdded = true;

        raised.Should().BeTrue();
    }

    [Fact(Skip = "Requires WinUI runtime (Application.Current.Resources)")]
    public void SettingXmlStatus_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep2XmlViewModel.XmlStatus))
                raised = true;
        };

        _sut.XmlStatus = "New status";

        raised.Should().BeTrue();
    }
}
