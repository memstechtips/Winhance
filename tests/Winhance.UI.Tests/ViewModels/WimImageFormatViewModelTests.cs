using FluentAssertions;
using Moq;
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
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
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
            _mockDialogService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object,
            _mockOfflineServicingService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
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
            _mockDialogService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object,
            _mockOfflineServicingService.Object);

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
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

    [Fact]
    public void Constructor_DefaultsToDuringSetup()
    {
        _sut.IsBuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void CurrentImageFormat_FillsTheEditionChoicesAndSelectsTheFirst()
    {
        _sut.CurrentImageFormat = Wim("Windows 11 Home", "Windows 11 Pro");

        _sut.EditionChoices.Select(e => e.Name).Should().Equal("Windows 11 Home", "Windows 11 Pro");
        _sut.SelectedEdition!.Name.Should().Be("Windows 11 Home");
    }

    [Fact]
    public void WorkingDirectoryChange_GoesBackToDuringSetup()
    {
        _sut.IsBuiltIntoImage = true;

        _sut.WorkingDirectory = "D:\\Other";

        _sut.IsBuiltIntoImage.Should().BeFalse();
    }

    [Fact]
    public void ShowMountFolderWarning_FolderDismCannotMountBesideInImageMode()
    {
        _sut.WorkingDirectory = "\\\\nas\\iso\\WinhanceWIM";
        _mockOfflineServicingService.Setup(s => s.CanMountFor(_sut.WorkingDirectory)).Returns(false);
        _sut.ShowMountFolderWarning.Should().BeFalse();

        _sut.IsBuiltIntoImage = true;
        _sut.ShowMountFolderWarning.Should().BeTrue();

        _sut.IsBuiltIntoImage = false;
        _sut.ShowMountFolderWarning.Should().BeFalse();
    }

    [Fact]
    public void ShowMountFolderWarning_LocalNtfsFolder_StaysClosed()
    {
        _sut.WorkingDirectory = "C:\\WorkDir";

        _sut.IsBuiltIntoImage = true;

        _sut.ShowMountFolderWarning.Should().BeFalse();
    }

}
