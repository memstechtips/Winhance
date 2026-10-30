using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.ViewModels;
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
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IAutounattendReader> _mockAutounattendReader = new();
    private readonly Mock<IOfflineServicingService> _mockOfflineServicingService = new();

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

        _mockOfflineServicingService.Setup(s => s.CanMountFor(It.IsAny<string>())).Returns(true);

        _sut = new WimImageFormatViewModel(
            _mockWimImageService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object,
            _mockFileSystemService.Object,
            _mockAutounattendReader.Object,
            _mockOfflineServicingService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

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

    [Fact]
    public async Task ConvertImageFormatCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimImageService.Verify(s => s.ConvertImageAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWimCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.DeleteWimCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimImageService.Verify(s => s.DeleteImageFileAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEsdCommand_WhenWorkingDirectoryEmpty_ShowsWarningAndReturns()
    {
        await _sut.DeleteEsdCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(
            "WIMUtil_Msg_WorkingDirectoryRequired",
            It.IsAny<string>()), Times.Once);
        _mockWimImageService.Verify(s => s.DeleteImageFileAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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

        _mockLogService.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Test error")), It.IsAny<Exception>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ConvertImageFormatCommand_WhenCurrentImageFormatIsNull_DoesNothing()
    {
        _sut.CurrentImageFormat = null;

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(
            It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task ConvertImageFormatCommand_WhenUserCancelsConfirmation_DoesNotConvert()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.ConvertImageAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConvertImageFormatCommand_OnSuccess_SetsConvertImageCardComplete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
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
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CurrentTaskCancellationSource)
            .Returns(new CancellationTokenSource());

        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());

        _mockWimImageService
            .Setup(s => s.ConvertImageAsync(
                It.IsAny<string>(), It.IsAny<ImageFormat>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _sut.ConvertImageCard.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteWimCommand_WhenUserCancels_DoesNotDelete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.DeleteWimCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.DeleteImageFileAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWimCommand_OnSuccess_RedetectsFormats()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

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

    [Fact]
    public async Task DeleteEsdCommand_WhenUserCancels_DoesNotDelete()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.DeleteEsdCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.DeleteImageFileAsync(
            It.IsAny<string>(), It.IsAny<ImageFormat>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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

    [Fact]
    public void IsImageLocked_DisablesTheConvertCardUntilItClears()
    {
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };
        _sut.UpdateConversionCardState();

        _sut.IsImageLocked = true;

        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();

        _sut.IsImageLocked = false;

        _sut.ConvertImageCard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ConvertImageFormatCommand_WhileTheImageIsLocked_DoesNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            FileSizeBytes = 4_000_000_000L
        };
        _sut.IsImageLocked = true;

        await _sut.ConvertImageFormatCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(
            It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockTaskProgressService.Verify(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommands_WhileTheImageIsLocked_DoNothing()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.IsImageLocked = true;

        await _sut.DeleteWimCommand.ExecuteAsync(null);
        await _sut.DeleteEsdCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(
            It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public void FormatFileSize_ConvertsToGB()
    {
        var result = WimImageFormatViewModel.FormatFileSize(4_294_967_296L);

        var sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        result.Should().Be($"4{sep}00 GB");
    }

    [Fact]
    public void FormatFileSize_HandlesZero()
    {
        var result = WimImageFormatViewModel.FormatFileSize(0);

        var sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        result.Should().Be($"0{sep}00 GB");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new WimImageFormatViewModel(
            _mockWimImageService.Object,
            _mockTaskProgressService.Object,
            _mockDialogService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object,
            _mockFileSystemService.Object,
            _mockAutounattendReader.Object,
            _mockOfflineServicingService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

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

    private static ImageFormatInfo Wim(params string[] editions) => new()
    {
        Format = ImageFormat.Wim,
        FileSizeBytes = 4_000_000_000L,
        Editions = editions.Select((name, i) => new ImageEdition(i + 1, name, name)).ToList(),
    };

    private void ArrangePrepare(EditionRemovalResult result)
    {
        _sut.WorkingDirectory = "C:\\WorkDir";
        _sut.CurrentImageFormat = Wim("Windows 11 Home", "Windows 11 Pro");
        _sut.SelectedEdition = _sut.EditionChoices[1];
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _mockTaskProgressService
            .Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new CancellationTokenSource());
        _mockTaskProgressService
            .Setup(t => t.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());
        _mockWimImageService
            .Setup(s => s.KeepEditionAsWimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _mockWimImageService
            .Setup(s => s.DetectAllImageFormatsAsync(It.IsAny<string>()))
            .ReturnsAsync(new ImageDetectionResult { WimInfo = Wim("Windows 11 Pro") });
    }

    [Fact]
    public void Constructor_ServicingDefaultsToDuringSetup()
    {
        _sut.ServicingChoices.Should().HaveCount(2);
        _sut.SelectedServicing!.BuiltIntoImage.Should().BeFalse();
        _sut.IsBuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void CurrentImageFormat_FillsTheEditionChoicesAndSelectsTheFirst()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Home", "Windows 11 Pro");

        _sut.EditionChoices.Select(e => e.Name).Should().Equal("Windows 11 Home", "Windows 11 Pro");
        _sut.SelectedEdition!.Name.Should().Be("Windows 11 Home");
    }

    [Theory]
    [InlineData(ImageFormat.Wim, 1, false, true)]
    [InlineData(ImageFormat.Wim, 2, false, false)]
    [InlineData(ImageFormat.Esd, 1, false, false)]
    [InlineData(ImageFormat.Wim, 1, true, false)]
    public void IsImageReadyForServicing_OnlyForOneEditionInAWim(ImageFormat format, int editions, bool bothExist, bool expected)
    {
        _sut.BothFormatsExist = bothExist;
        _sut.CurrentImageFormat = Wim(Enumerable.Range(1, editions).Select(i => $"Edition {i}").ToArray()) with { Format = format };

        _sut.IsImageReadyForServicing.Should().Be(expected);
    }

    [Fact]
    public void WorkingDirectoryChange_GoesBackToDuringSetup()
    {
        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);

        _sut.WorkingDirectory = "D:\\Other";

        _sut.IsBuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void ImageMode_DisablesTheConvertCard()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Pro");
        _sut.UpdateConversionCardState();
        _sut.ConvertImageCard.IsEnabled.Should().BeTrue();

        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);

        _sut.ConvertImageCard.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ShowHostDismWarning_ImageNewerThanThisPcInImageMode()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Pro") with { Build = int.MaxValue };
        _sut.ShowHostDismWarning.Should().BeFalse();

        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);

        _sut.ShowHostDismWarning.Should().BeTrue();
    }

    [Fact]
    public void ShowMountFolderWarning_FolderDismCannotMountBesideInImageMode()
    {
        _sut.WorkingDirectory = "\\\\nas\\iso\\WinhanceWIM";
        _mockOfflineServicingService.Setup(s => s.CanMountFor(_sut.WorkingDirectory)).Returns(false);
        _sut.ShowMountFolderWarning.Should().BeFalse();

        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);
        _sut.ShowMountFolderWarning.Should().BeTrue();

        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => !c.BuiltIntoImage);
        _sut.ShowMountFolderWarning.Should().BeFalse();
    }

    [Fact]
    public void ShowMountFolderWarning_LocalNtfsFolder_StaysClosed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);

        _sut.ShowMountFolderWarning.Should().BeFalse();
    }

    [Fact]
    public async Task PrepareImageForServicingCommand_KeepsTheSelectedEditionAndRedetects()
    {
        ArrangePrepare(new EditionRemovalResult(true));

        await _sut.PrepareImageForServicingCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.KeepEditionAsWimAsync("C:\\WorkDir", "Windows 11 Pro",
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        _sut.IsImageReadyForServicing.Should().BeTrue();
        _sut.IsConverting.Should().BeFalse();
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Never);
    }

    [Fact]
    public async Task PrepareImageForServicingCommand_UserDeclines_ChangesNothing()
    {
        ArrangePrepare(new EditionRemovalResult(true));
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        await _sut.PrepareImageForServicingCommand.ExecuteAsync(null);

        _mockWimImageService.Verify(s => s.KeepEditionAsWimAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareImageForServicingCommand_Failed_ShowsAnErrorAndResetsIsConverting()
    {
        ArrangePrepare(EditionRemovalResult.Failed);

        await _sut.PrepareImageForServicingCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Once);
        _mockDialogService.Verify(d => d.ShowErrorAsync("WIMUtil_Msg_PrepareServicing_Failed", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _sut.IsConverting.Should().BeFalse();
    }

    [Fact]
    public void PrepareImageForServicingCommand_WhileTheImageIsLocked_CannotRun()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Home", "Windows 11 Pro");
        _sut.PrepareImageForServicingCommand.CanExecute(null).Should().BeTrue();

        _sut.IsImageLocked = true;

        _sut.PrepareImageForServicingCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void PrepareImageForServicingCommand_WhenBothFormatsExist_CannotRun()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Home", "Windows 11 Pro");

        _sut.BothFormatsExist = true;

        _sut.PrepareImageForServicingCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void PrepareImageForServicingCommand_WhenTheImageIsAlreadyReady_CannotRun()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Pro");

        _sut.PrepareImageForServicingCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task PrepareImageForServicingCommand_NotEnoughSpace_ShowsTheSpaceMessage()
    {
        ArrangePrepare(new EditionRemovalResult(true));
        _mockWimImageService
            .Setup(s => s.KeepEditionAsWimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientDiskSpaceException("D:", 20, 5, "Edition removal"));

        await _sut.PrepareImageForServicingCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync("WIMUtil_Msg_InsufficientSpace_Prepare", "WIMUtil_Status_InsufficientDiskSpace", It.IsAny<string>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sut.IsConverting.Should().BeFalse();
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task PrepareImageForServicingCommand_WarnsOnlyWhenTheAnswerFilePicksAnotherEdition(int option, bool warns)
    {
        ArrangePrepare(new EditionRemovalResult(true));
        _sut.CurrentImageFormat = new ImageFormatInfo
        {
            Format = ImageFormat.Wim,
            Editions = [new ImageEdition(1, "Windows 11 Home", "Core"), new ImageEdition(2, "Windows 11 Pro", "Professional")],
        };
        _sut.SelectedEdition = _sut.EditionChoices[1];
        ArrangeAnswerFilePicking(option);

        await _sut.PrepareImageForServicingCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), "WIMUtil_Generate_Plan_ChosenNotOnMedia", It.IsAny<string>()),
            warns ? Times.Once() : Times.Never());
    }

    [Fact]
    public void Constructor_LeavesTheConvertCardDetecting()
    {
        _sut.ConvertImageCard.Description.Should().Be("WIMUtil_Label_Detecting");
    }

    [Fact]
    public void RefreshLocalizedText_NullFromTheClearedComboBox_DoesNotFlipTheMode()
    {
        _sut.SelectedServicing = _sut.ServicingChoices.Single(c => c.BuiltIntoImage);
        _sut.ServicingChoices.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                _sut.SelectedServicing = null;
        };
        var seen = new List<bool>();
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WimImageFormatViewModel.IsBuiltIntoImage))
                seen.Add(_sut.IsBuiltIntoImage);
        };

        _sut.RefreshLocalizedText();

        seen.Should().NotContain(false);
        _sut.IsBuiltIntoImage.Should().BeTrue();
    }

    private void ArrangeAnswerFilePicking(int option)
    {
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _mockFileSystemService.Setup(f => f.FileExists("C:\\WorkDir\\autounattend.xml")).Returns(true);
        var config = new WinhanceConfigFile
        {
            Autounattend = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["autounattend"] = new() { Items = [new ConfigurationItem { Id = "autounattend-edition", SelectedIndex = option }] },
                },
            },
        };
        _mockAutounattendReader
            .Setup(r => r.ReadAsync("C:\\WorkDir\\autounattend.xml"))
            .ReturnsAsync(new WinhanceAnswerFile("26.10", config));
    }
}
