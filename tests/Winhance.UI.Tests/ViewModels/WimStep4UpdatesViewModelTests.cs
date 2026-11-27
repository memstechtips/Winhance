using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimStep4UpdatesViewModelTests
{
    private const string WorkDir = "C:\\WorkDir";

    private readonly Mock<IWimCustomizationService> _mockWimCustomizationService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IWindowsUpdateListService> _mockUpdateListService = new();
    private readonly Mock<IWindowsUpdateStager> _mockUpdateStager = new();

    private readonly WimStep4UpdatesViewModel _sut;

    public WimStep4UpdatesViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);

        _sut = new WimStep4UpdatesViewModel(
            _mockWimCustomizationService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object,
            _mockUpdateListService.Object,
            _mockUpdateStager.Object);
    }

    private static IProgress<TaskProgressDetail> Progress => new Progress<TaskProgressDetail>();

    private static readonly ImageFormatInfo X64Media = new() { Build = 26100, Ubr = 1742, Architecture = "x64" };

    private static WindowsUpdate Offer() => new(
        "KB5099999", "2026-10 Cumulative Update", 2000, new DateOnly(2026, 10, 14), "cat-id",
        [new WindowsUpdateFile("target", "KB5099999", "update.msu", "https://example.test/update.msu", 1_500_000_000, "sha1", "sha256")]);

    private void ArrangeOffer(WindowsUpdate? offer) =>
        _mockUpdateListService
            .Setup(s => s.FindUpdateAsync(26100, 1742, "x64", It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

    private void ArrangeStaged(UpdateStageResult result) =>
        _mockUpdateStager
            .Setup(s => s.StageAsync(WorkDir, It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public void Constructor_StartsUncheckedAndNotOfferable()
    {
        _sut.AddUpdate.Should().BeFalse();
        _sut.CanAddUpdate.Should().BeFalse();
        _sut.NeedsApply.Should().BeFalse();
        _sut.WorkingDirectory.Should().BeEmpty();
        _sut.ApplyTitleKey.Should().Be("WIMUtil_Build_PrepareUpdate");
    }

    [Fact]
    public async Task SetMediaAsync_WhenOfferFound_AllowsTheChoiceWithoutTickingIt()
    {
        ArrangeOffer(Offer());

        await _sut.SetMediaAsync(X64Media);

        _sut.CanAddUpdate.Should().BeTrue();
        _sut.AddUpdate.Should().BeFalse();
        _mockLocalizationService.Verify(l => l.GetString(
            "WIMUtil_Card_AddUpdate_Description_Offer", It.Is<object[]>(a => a.Length == 3 && (string)a[0] == "KB5099999")), Times.Once);
    }

    [Fact]
    public async Task SetMediaAsync_WhenNoOffer_CannotBeChosen()
    {
        ArrangeOffer(null);

        await _sut.SetMediaAsync(X64Media);

        _sut.CanAddUpdate.Should().BeFalse();
        _sut.UpdateDescription.Should().Be("WIMUtil_Card_AddUpdate_Description_None");
    }

    [Theory]
    [InlineData(0, "x64")]
    [InlineData(26100, "x86")]
    [InlineData(26100, "")]
    public async Task SetMediaAsync_WhenBuildOrArchitectureUnsupported_SkipsTheLookup(int build, string architecture)
    {
        await _sut.SetMediaAsync(new ImageFormatInfo { Build = build, Ubr = 1, Architecture = architecture });

        _sut.CanAddUpdate.Should().BeFalse();
        _mockUpdateListService.Verify(s => s.FindUpdateAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetMediaAsync_WhenAlreadyStaged_ShowsItChosenWithNothingToApply()
    {
        _sut.WorkingDirectory = WorkDir;
        _mockUpdateStager.Setup(s => s.IsStaged(WorkDir)).Returns(true);
        ArrangeOffer(Offer());

        await _sut.SetMediaAsync(X64Media);

        _sut.AddUpdate.Should().BeTrue();
        _sut.NeedsApply.Should().BeFalse();
        _sut.UpdateDescription.Should().Be("WIMUtil_Card_AddUpdate_Description_Staged");
    }

    [Fact]
    public async Task TickingTheBox_NeedsApplyButStagesNothing()
    {
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);

        _sut.AddUpdate = true;

        _sut.NeedsApply.Should().BeTrue();
        _mockUpdateStager.Verify(s => s.StageAsync(
            It.IsAny<string>(), It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_StagesTheUpdateAndAddsTheInstallStep()
    {
        _sut.WorkingDirectory = WorkDir;
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.AddUpdate = true;
        ArrangeStaged(UpdateStageResult.Staged);

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().BeNull();
        _mockWimCustomizationService.Verify(s => s.EnsureUpdateInstallStepAsync(WorkDir, It.IsAny<CancellationToken>()), Times.Once);
        _sut.NeedsApply.Should().BeFalse();
    }

    [Theory]
    [InlineData(UpdateStageResult.DownloadFailed, "WIMUtil_Msg_UpdateDownloadFailed")]
    [InlineData(UpdateStageResult.HashMismatch, "WIMUtil_Msg_UpdateHashMismatch")]
    [InlineData(UpdateStageResult.SignatureInvalid, "WIMUtil_Msg_UpdateSignatureInvalid")]
    public async Task ApplyAsync_WhenStagingFails_ReturnsItsMessageAndSkipsTheInstallStep(UpdateStageResult result, string messageKey)
    {
        _sut.WorkingDirectory = WorkDir;
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.AddUpdate = true;
        ArrangeStaged(result);

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().Be(messageKey);
        _sut.NeedsApply.Should().BeTrue();
        _mockWimCustomizationService.Verify(s => s.EnsureUpdateInstallStepAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_WhenStagerThrows_ReturnsTheErrorMessage()
    {
        _sut.WorkingDirectory = WorkDir;
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.AddUpdate = true;
        _mockUpdateStager
            .Setup(s => s.StageAsync(WorkDir, It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var failure = await _sut.ApplyAsync(Progress, CancellationToken.None);

        failure.Should().Be("WIMUtil_Msg_UpdateError");
    }

    [Fact]
    public async Task ApplyAsync_WhenCancelled_Rethrows()
    {
        _sut.WorkingDirectory = WorkDir;
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.AddUpdate = true;
        _mockUpdateStager
            .Setup(s => s.StageAsync(WorkDir, It.IsAny<WindowsUpdate>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.ApplyAsync(Progress, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task IsBuiltIntoImage_StagedUpdateSaysCreateBuildsItIn()
    {
        _sut.WorkingDirectory = WorkDir;
        _mockUpdateStager.Setup(s => s.IsStaged(WorkDir)).Returns(true);
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);

        _sut.IsBuiltIntoImage = true;

        _sut.UpdateDescription.Should().Be("WIMUtil_Card_AddUpdate_Description_Staged_Image");
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

        _sut.UpdateDescription.Should().Be("WIMUtil_Card_AddUpdate_Description_Looking");

        lookup.SetResult(Offer());
        await setting;

        _sut.UpdateDescription.Should().Be("WIMUtil_Card_AddUpdate_Description_Offer_Image");
    }

    [Fact]
    public async Task ShowUpdateState_AfterServicingTookTheUpdate_ClearsTheChoice()
    {
        _sut.WorkingDirectory = WorkDir;
        _mockUpdateStager.Setup(s => s.IsStaged(WorkDir)).Returns(true);
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _mockUpdateStager.Setup(s => s.IsStaged(WorkDir)).Returns(false);

        _sut.ShowUpdateState();

        _sut.AddUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task WorkingDirectory_WhenChanged_ClearsTheChoice()
    {
        ArrangeOffer(Offer());
        await _sut.SetMediaAsync(X64Media);
        _sut.AddUpdate = true;

        _sut.WorkingDirectory = "C:\\Other";

        _sut.AddUpdate.Should().BeFalse();
    }
}
