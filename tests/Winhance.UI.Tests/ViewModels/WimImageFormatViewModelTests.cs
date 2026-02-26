using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WimImageFormatViewModelTests : IDisposable
{
    private readonly Mock<IWimImageService> _mockWimImageService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly WimImageFormatViewModel _sut;

    public WimImageFormatViewModelTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(a => a().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _sut = new WimImageFormatViewModel(
            _mockWimImageService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_InitializesConversionStatusToEmpty()
    {
        _sut.ConversionStatus.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesWimFileSizeToEmpty()
    {
        _sut.WimFileSize.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesEsdFileSizeToEmpty()
    {
        _sut.EsdFileSize.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesWorkingDirectoryToEmpty()
    {
        _sut.WorkingDirectory.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_InitializesShowConversionCardToFalse()
    {
        _sut.ShowConversionCard.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesIsConvertingToFalse()
    {
        _sut.IsConverting.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesConvertImageCard()
    {
        _sut.ConvertImageCard.Should().NotBeNull();
        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();
    }

    // ── DetectImageFormatAsync ──

    [Fact]
    public async Task DetectImageFormatAsync_WhenWimFormatDetected_SetsCurrentImageFormat()
    {
        var wimInfo = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FilePath = "C:\\sources\\install.wim",
            FileSizeBytes = 4_000_000_000L
        };
        var detection = new ImageDetectionResult { WimInfo = wimInfo };

        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(detection);

        _sut.WorkingDirectory = "C:\\WorkDir";

        await _sut.DetectImageFormatAsync();

        _sut.CurrentImageFormat.Should().NotBeNull();
        _sut.CurrentImageFormat!.Format.Should().Be(ImageFormat.Wim);
        _sut.ShowConversionCard.Should().BeTrue();
    }

    [Fact]
    public async Task DetectImageFormatAsync_WhenBothFormatsExist_SetsBothFormatsExist()
    {
        var wimInfo = new ImageFormatInfo { Format = ImageFormat.Wim, FileSizeBytes = 4_000_000_000L };
        var esdInfo = new ImageFormatInfo { Format = ImageFormat.Esd, FileSizeBytes = 2_600_000_000L };
        var detection = new ImageDetectionResult { WimInfo = wimInfo, EsdInfo = esdInfo };

        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(detection);

        await _sut.DetectImageFormatAsync();

        _sut.BothFormatsExist.Should().BeTrue();
        _sut.WimFileSize.Should().NotBeEmpty();
        _sut.EsdFileSize.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DetectImageFormatAsync_WhenNoFormatDetected_HidesConversionCard()
    {
        var detection = new ImageDetectionResult();

        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(detection);

        await _sut.DetectImageFormatAsync();

        _sut.ShowConversionCard.Should().BeFalse();
    }

    [Fact]
    public async Task DetectImageFormatAsync_WhenExceptionThrown_HidesConversionCard()
    {
        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Detection error"));

        await _sut.DetectImageFormatAsync();

        _sut.ShowConversionCard.Should().BeFalse();
    }

    // ── SafeDetectImageFormatAsync ──

    [Fact]
    public async Task SafeDetectImageFormatAsync_SwallowsExceptions()
    {
        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Unhandled error"));

        var act = () => _sut.SafeDetectImageFormatAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SafeDetectImageFormatAsync_LogsError()
    {
        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test error"));

        await _sut.SafeDetectImageFormatAsync();

        _mockLogService.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Test error")), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    // ── ConvertImageFormat command ──

    [Fact]
    public async Task ConvertImageFormatCommand_WhenCurrentImageFormatIsNull_DoesNothing()
    {
        _sut.CurrentImageFormat = null;

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConvertImageFormatCommand_WhenUserCancelsConfirmation_DoesNotConvert()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.ConvertImageAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConvertImageFormatCommand_OnSuccess_SetsConvertImageCardComplete()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockWimImageService
            .Setup(s => s.ConvertImageAsync(
                It.IsAny<string>(), It.IsAny<ImageFormat>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup DetectAllImageFormatsAsync for the re-detection after conversion
        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult
            {
                EsdInfo = new ImageFormatInfo { Format = ImageFormat.Esd, FileSizeBytes = 2_600_000_000L }
            });

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _sut.ConvertImageCard.IsComplete.Should().BeTrue();
        _sut.IsConverting.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertImageFormatCommand_OnFailure_SetsConvertImageCardHasFailed()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreatePowerShellProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockWimImageService
            .Setup(s => s.ConvertImageAsync(
                It.IsAny<string>(), It.IsAny<ImageFormat>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _sut.ConvertImageCard.HasFailed.Should().BeTrue();
    }

    // ── DeleteWim command ──

    [Fact]
    public async Task DeleteWimCommand_WhenUserCancels_DoesNotDelete()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.DeleteWimCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.DeleteImageFileAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWimCommand_OnSuccess_RedetectsFormats()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockWimImageService
            .Setup(s => s.DeleteImageFileAsync(
                It.IsAny<string>(), ImageFormat.Wim,
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult());

        await _sut.DeleteWimCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()), Times.Once);
    }

    // ── DeleteEsd command ──

    [Fact]
    public async Task DeleteEsdCommand_WhenUserCancels_DoesNotDelete()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.DeleteEsdCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.DeleteImageFileAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── UpdateConversionCardState ──

    [Fact]
    public void UpdateConversionCardState_WhenBothFormatsExist_DisablesCard()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };
        _sut.BothFormatsExist = true;

        _sut.UpdateConversionCardState();

        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateConversionCardState_WhenCurrentImageFormatIsNull_DisablesCard()
    {
        _sut.CurrentImageFormat = null;

        _sut.UpdateConversionCardState();

        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateConversionCardState_WhenSingleWimFormat_EnablesCard()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };
        _sut.BothFormatsExist = false;

        _sut.UpdateConversionCardState();

        _sut.ConvertImageCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateConversionCardState_WhenConverting_DisablesCard()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };
        _sut.BothFormatsExist = false;
        _sut.IsConverting = true;

        _sut.UpdateConversionCardState();

        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();
    }

    // ── FormatFileSize ──

    [Fact]
    public void FormatFileSize_ConvertsToGB()
    {
        var result = WimImageFormatViewModel.FormatFileSize(4_294_967_296L); // 4 GB

        result.Should().Be("4.00 GB");
    }

    [Fact]
    public void FormatFileSize_HandlesZero()
    {
        var result = WimImageFormatViewModel.FormatFileSize(0);

        result.Should().Be("0.00 GB");
    }

    // ── IDisposable ──

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimImageFormatViewModel(
            _mockWimImageService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    // ── Property change notifications ──

    [Fact]
    public void SettingIsConverting_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimImageFormatViewModel.IsConverting))
                raised = true;
        };

        _sut.IsConverting = true;

        raised.Should().BeTrue();
    }

    [Fact]
    public void SettingShowConversionCard_RaisesPropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimImageFormatViewModel.ShowConversionCard))
                raised = true;
        };

        _sut.ShowConversionCard = true;

        raised.Should().BeTrue();
    }
}
