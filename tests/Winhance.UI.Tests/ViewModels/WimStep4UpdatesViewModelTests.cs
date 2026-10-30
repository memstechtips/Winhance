using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep4UpdatesViewModelTests
{
    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IResourceService> _mockResourceService = new();
    private readonly Mock<IAnswerFileValidator> _mockAnswerFileValidator = new();
    private readonly Mock<IWindowsUpdateListService> _mockUpdateListService = new();
    private readonly Mock<IWindowsUpdateStager> _mockUpdateStager = new();
    private readonly AnswerFileCheckState _checkState = new();

    private readonly WimStep4UpdatesViewModel _sut;

    public WimStep4UpdatesViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockAnswerFileValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([]));

        _sut = new WimStep4UpdatesViewModel(
            _mockWimCustomizationService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockFileSystemService.Object,
            _mockLogService.Object,
            _mockResourceService.Object,
            _mockAnswerFileValidator.Object,
            _checkState,
            _mockUpdateListService.Object,
            _mockUpdateStager.Object);
    }

    [Fact]
    public void Constructor_InitializesCardDisabledAndNothingAdded()
    {
        _sut.AddWindowsUpdateCard.Should().NotBeNull();
        _sut.AddWindowsUpdateCard.IsEnabled.Should().BeFalse();
        _sut.IsUpdateAdded.Should().BeFalse();
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    private static readonly ImageFormatInfo X64Media = new() { Build = 26100, Ubr = 1742, Architecture = "x64" };

    private static WindowsUpdate Offer() => new(
        "KB5099999", "2026-10 Cumulative Update", 2000, new DateOnly(2026, 10, 14), "cat-id",
        [new WindowsUpdateFile("target", "KB5099999", "update.msu", "https://example.test/update.msu", 1_500_000_000, "sha1", "sha256")]);

    private void ArrangeOffer(WindowsUpdate? offer) =>
        _mockUpdateListService
            .Setup(s => s.FindUpdateAsync(26100, 1742, "x64", It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

    [Fact]
    public async Task SetMediaAsync_WhenOfferFound_EnablesCardAndLooksUpWithMediaBuild()
    {
        ArrangeOffer(Offer());

        await _sut.SetMediaAsync(X64Media);

        _sut.AddWindowsUpdateCard.IsEnabled.Should().BeTrue();
        _sut.AddWindowsUpdateCard.IsComplete.Should().BeFalse();
        _mockLocalizationService.Verify(l => l.GetString(
            "WIMUtil_Card_AddUpdate_Description_Offer", It.Is<object[]>(a => a.Length == 3 && (string)a[0] == "KB5099999")), Times.Once);
    }

    [Fact]
    public async Task SetMediaAsync_WhenNoOffer_DisablesCard()
    {
        ArrangeOffer(null);

        await _sut.SetMediaAsync(X64Media);

        _sut.AddWindowsUpdateCard.IsEnabled.Should().BeFalse();
        _sut.AddWindowsUpdateCard.Description.Should().Be("WIMUtil_Card_AddUpdate_Description_None");
    }

    [Theory]
    [InlineData(0, "x64")]
    [InlineData(26100, "x86")]
    [InlineData(26100, "")]
    public async Task SetMediaAsync_WhenBuildOrArchitectureUnsupported_DisablesCardWithoutLookup(int build, string architecture)
    {
        await _sut.SetMediaAsync(new ImageFormatInfo { Build = build, Ubr = 1, Architecture = architecture });

        _sut.AddWindowsUpdateCard.IsEnabled.Should().BeFalse();
        _mockUpdateListService.Verify(s => s.FindUpdateAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetMediaAsync_WhenAlreadyStaged_ShowsCardAsComplete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockUpdateStager.Setup(s => s.IsStaged("C:\\WorkDir")).Returns(true);
        ArrangeOffer(Offer());

        await _sut.SetMediaAsync(X64Media);

        _sut.AddWindowsUpdateCard.IsComplete.Should().BeTrue();
        _sut.AddWindowsUpdateCard.Description.Should().Be("WIMUtil_Card_AddUpdate_Description_Staged");
    }

    [Fact]
    public async Task AddWindowsUpdateCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndDoesNotStage()
    {
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);

        await _sut.AddWindowsUpdateCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired", It.IsAny<string>()), Times.Once);
        _mockUpdateStager.Verify(s => s.StageAsync(
            It.IsAny<string>(), It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UpdateStageResult.DownloadFailed, "WIMUtil_Msg_UpdateDownloadFailed")]
    [InlineData(UpdateStageResult.HashMismatch, "WIMUtil_Msg_UpdateHashMismatch")]
    [InlineData(UpdateStageResult.SignatureInvalid, "WIMUtil_Msg_UpdateSignatureInvalid")]
    public async Task AddWindowsUpdateCommand_WhenStagingFails_ShowsItsErrorAndSkipsInstallStep(UpdateStageResult result, string messageKey)
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _mockUpdateStager
            .Setup(s => s.StageAsync(It.IsAny<string>(), It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        await _sut.AddWindowsUpdateCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync(messageKey, It.IsAny<string>()), Times.Once);
        _sut.AddWindowsUpdateCard.HasFailed.Should().BeTrue();
        _sut.AddWindowsUpdateCard.IsEnabled.Should().BeTrue();
        _mockWimCustomizationService.Verify(s => s.EnsureUpdateInstallStepAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddWindowsUpdateCommand_WhenStaged_EnsuresInstallStepAndShowsSuccess()
    {
        ArrangeStageContext();
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _mockUpdateStager
            .Setup(s => s.StageAsync("C:\\WorkDir", It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateStageResult.Staged);
        _mockWimCustomizationService
            .Setup(s => s.EnsureUpdateInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateInstallStepResult.Added);

        await _sut.AddWindowsUpdateCommand.ExecuteAsync(null);

        _mockWimCustomizationService.Verify(s => s.EnsureUpdateInstallStepAsync(
            "C:\\WorkDir", It.IsAny<CancellationToken>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowInformationAsync("WIMUtil_Msg_UpdateAdded", It.IsAny<string>()), Times.Once);
        _sut.AddWindowsUpdateCard.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task AddWindowsUpdateCommand_WhenStagedInImageMode_SaysItGoesIntoTheImage()
    {
        ArrangeStageContext();
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.IsBuiltIntoImage = true;
        _mockUpdateStager
            .Setup(s => s.StageAsync("C:\\WorkDir", It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateStageResult.Staged);
        _mockWimCustomizationService
            .Setup(s => s.EnsureUpdateInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateInstallStepResult.Added);

        await _sut.AddWindowsUpdateCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowInformationAsync("WIMUtil_Msg_UpdateAdded_Image", It.IsAny<string>()), Times.Once);
    }

    private void ArrangeStageContext()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
    }

    private async Task StageSuccessfullyAsync(UpdateInstallStepResult stepResult)
    {
        ArrangeStageContext();
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _mockUpdateStager
            .Setup(s => s.StageAsync("C:\\WorkDir", It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateStageResult.Staged);
        _mockWimCustomizationService
            .Setup(s => s.EnsureUpdateInstallStepAsync("C:\\WorkDir", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stepResult);

        await _sut.AddWindowsUpdateCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task AddWindowsUpdateCommand_WhenStaged_SetsIsUpdateAddedAndRaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimStep4UpdatesViewModel.IsUpdateAdded))
                raised = true;
        };

        await StageSuccessfullyAsync(UpdateInstallStepResult.Added);

        _sut.IsUpdateAdded.Should().BeTrue();
        raised.Should().BeTrue();
    }

    [Fact]
    public async Task AddWindowsUpdateCommand_WhenStagingFails_LeavesIsUpdateAddedFalse()
    {
        ArrangeStageContext();
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _mockUpdateStager
            .Setup(s => s.StageAsync(It.IsAny<string>(), It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateStageResult.DownloadFailed);

        await _sut.AddWindowsUpdateCommand.ExecuteAsync(null);

        _sut.IsUpdateAdded.Should().BeFalse();
    }

    [Theory]
    [InlineData(UpdateInstallStepResult.Added)]
    [InlineData(UpdateInstallStepResult.CreatedXml)]
    public async Task AddWindowsUpdateCommand_WhenInstallStepChangedTheAnswerFile_ChecksIt(UpdateInstallStepResult stepResult)
    {
        await StageSuccessfullyAsync(stepResult);

        _mockAnswerFileValidator.Verify(v => v.ValidateAsync("C:\\WorkDir\\autounattend.xml", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddWindowsUpdateCommand_WhenInstallStepAlreadyPresent_DoesNotCheckTheAnswerFile()
    {
        await StageSuccessfullyAsync(UpdateInstallStepResult.AlreadyPresent);

        _sut.IsUpdateAdded.Should().BeTrue();
        _mockAnswerFileValidator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsBuiltIntoImage_StagedUpdateSaysStep5BuildsItIn()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockUpdateStager.Setup(s => s.IsStaged("C:\\WorkDir")).Returns(true);
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);

        _sut.IsBuiltIntoImage = true;

        _sut.AddWindowsUpdateCard.Description.Should().Be("WIMUtil_Card_AddUpdate_Description_Staged_Image");
    }

    [Fact]
    public async Task IsBuiltIntoImage_DuringTheLookup_KeepsLookingThenShowsTheImageWording()
    {
        var lookup = new TaskCompletionSource<WindowsUpdate?>();
        _mockUpdateListService
            .Setup(s => s.FindUpdateAsync(26100, 1742, "x64", It.IsAny<CancellationToken>()))
            .Returns(lookup.Task);
        var setting = _sut.SetMediaAsync(X64Media);

        _sut.IsBuiltIntoImage = true;
        _sut.ShowUpdateState();

        _sut.AddWindowsUpdateCard.Description.Should().Be("WIMUtil_Card_AddUpdate_Description_Looking");

        lookup.SetResult(Offer());
        await setting;

        _sut.AddWindowsUpdateCard.Description.Should().Be("WIMUtil_Card_AddUpdate_Description_Offer_Image");
    }

    [Fact]
    public async Task IsBuiltIntoImage_Unchanged_LeavesTheCardTextAlone()
    {
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.AddWindowsUpdateCard.Description = "Custom";

        _sut.IsBuiltIntoImage = false;

        _sut.AddWindowsUpdateCard.Description.Should().Be("Custom");
    }

    [Fact]
    public async Task ShowUpdateState_AfterServicingTookTheUpdate_ShowsItNotAdded()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockUpdateStager.Setup(s => s.IsStaged("C:\\WorkDir")).Returns(true);
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _mockUpdateStager.Setup(s => s.IsStaged("C:\\WorkDir")).Returns(false);

        _sut.ShowUpdateState();

        _sut.IsUpdateAdded.Should().BeFalse();
        _sut.AddWindowsUpdateCard.IsComplete.Should().BeFalse();
    }
}
