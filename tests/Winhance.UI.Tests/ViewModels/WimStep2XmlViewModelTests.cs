using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep2XmlViewModelTests : IDisposable
{
    private const string GeneratedXmlPath = "C:\\WorkDir\\autounattend.xml";

    private readonly Mock<ISelectionSaveService> _mockSaves = new();
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ISelectionSetBuilder> _mockSelections = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IResourceService> _mockResourceService = new();

    private readonly WimStep2XmlViewModel _sut;

    public WimStep2XmlViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        // Default: a set carrying a Windows app so generate doesn't show the no-apps warning
        _mockSelections
            .Setup(s => s.FromMachineAsync())
            .ReturnsAsync(new SelectionSet(
                Array.Empty<SettingChoice>(),
                [new AppChoice("test", "Test", null, null, null, null)],
                Array.Empty<AppChoice>(),
                AutounattendChoices.None));

        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(GeneratedXmlPath);

        _sut = new WimStep2XmlViewModel(
            _mockSaves.Object,
            _mockWimCustomizationService.Object,
            _mockSelections.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockResourceService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ArrangeGenerateConfirmed() =>
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

    [Fact]
    public void Constructor_InitializesSelectedXmlPathToEmpty()
    {
        _sut.SelectedXmlPath.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesXmlStatusFromLocalization()
    {
        _sut.XmlStatus.Should().Be("WIMUtil_Status_NoXmlAdded");
    }

    [Fact]
    public void Constructor_InitializesIsXmlAddedToFalse()
    {
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesActionCards()
    {
        _sut.GenerateWinhanceXmlCard.Should().NotBeNull();
        _sut.DownloadXmlCard.Should().NotBeNull();
        _sut.SelectXmlCard.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_AllActionCardsAreEnabled()
    {
        _sut.GenerateWinhanceXmlCard.IsEnabled.Should().BeTrue();
        _sut.DownloadXmlCard.IsEnabled.Should().BeTrue();
        _sut.SelectXmlCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WorkingDirectoryDefaultsToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    // The empty-WorkingDirectory guard (issue #506).

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.DownloadUnattendedWinstallXmlAsync(
            It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockSaves.Verify(s => s.SaveAsync(
            It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimCustomizationService.Verify(s => s.AddXmlToImageAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenUserCancels_DoesNotGenerate()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(
            It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()), Times.Never);
        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_SetsIsXmlAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeGenerateConfirmed();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.SelectedXmlPath.Should().Be(GeneratedXmlPath);
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WritesIntoTheWorkingDirectory_WithoutASuccessDialog()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeGenerateConfirmed();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockSaves.Verify(s => s.SaveAsync(
            BuilderTarget.Autounattend,
            It.IsAny<SelectionSet>(),
            It.Is<SelectionSaveOptions>(o => o.FixedPath == GeneratedXmlPath && !o.ReportSuccessInDialog)),
            Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_EnsuresTheDriverInstallStep()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeGenerateConfirmed();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(
            s => s.EnsureDriverInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_EnsureFailure_DoesNotFailTheFlow()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeGenerateConfirmed();
        _mockWimCustomizationService
            .Setup(s => s.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeTrue();
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenUserCancels_DoesNotEnsureTheDriverInstallStep()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(
            s => s.EnsureDriverInstallStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_WhenNothingWasSaved_LeavesTheCardIncomplete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeGenerateConfirmed();
        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync((string?)null);

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnSuccess_ClearsOtherCardCompletions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.DownloadXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        ArrangeGenerateConfirmed();

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.DownloadXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWinhanceXmlCommand_OnException_SetsHasFailed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeGenerateConfirmed();

        _mockSaves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ThrowsAsync(new Exception("Generation failed"));

        await _sut.GenerateWinhanceXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.HasFailed.Should().BeTrue();
        _mockDialogService.Verify(d => d.ShowErrorAsync(
            "WIMUtil_Msg_XmlGenError", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_OnSuccess_ClearsOtherCardCompletions()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.GenerateWinhanceXmlCard.IsComplete = true;
        _sut.SelectXmlCard.IsComplete = true;

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("C:\\WorkDir\\autounattend.xml");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _sut.GenerateWinhanceXmlCard.IsComplete.Should().BeFalse();
        _sut.SelectXmlCard.IsComplete.Should().BeFalse();
        _sut.DownloadXmlCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlCommand_PassesCorrectDestinationPath()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _mockWimCustomizationService
            .Setup(s => s.DownloadUnattendedWinstallXmlAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<TaskProgressDetail>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("C:\\WorkDir\\autounattend.xml");

        _mockWimCustomizationService
            .Setup(s => s.AddXmlToImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        await _sut.DownloadUnattendedWinstallXmlCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.DownloadUnattendedWinstallXmlAsync(
            "C:\\WorkDir\\autounattend.xml",
            It.IsAny<IProgress<TaskProgressDetail>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenCancelled_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns((string?)null);

        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
    public async Task SelectXmlFileCommand_WhenEmptyString_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFilePickerService
            .Setup(f => f.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        await _sut.SelectXmlFileCommand.ExecuteAsync(null);

        _sut.IsXmlAdded.Should().BeFalse();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimStep2XmlViewModel(
            _mockSaves.Object,
            _mockWimCustomizationService.Object,
            _mockSelections.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockFilePickerService.Object,
            _mockLogService.Object,
            _mockResourceService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
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

    [Fact]
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
