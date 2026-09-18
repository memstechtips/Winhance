using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WindowsThemeServiceTests
{
    private const string DesktopKey = @"HKEY_CURRENT_USER\Control Panel\Desktop";
    private const string WallpapersKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
    private const string ColorsKey = @"HKEY_CURRENT_USER\Control Panel\Colors";
    private const string SlideshowKey = @"HKEY_CURRENT_USER\Control Panel\Personalization\Desktop Slideshow";
    private const string Album = @"D:\Pictures\Holiday";
    private const string Transcoded = @"C:\Users\x\AppData\Roaming\Microsoft\Windows\Themes\TranscodedWallpaper";
    private static readonly byte[] AlbumId = [0x02, 0x00, 0x14, 0x1F];
    private const int ColorBackground = 1;
    private const int DefaultIntervalMs = 1800000;

    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<ISystemParametersService> _systemParameters = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IDesktopSlideshow> _slideshow = new();
    private readonly Mock<IInteractiveUserService> _interactiveUser = new();
    private readonly FakeDetectionContext _shell = new FakeDetectionContext().Folder(AlbumId, Album);
    private readonly WindowsThemeService _sut;

    public WindowsThemeServiceTests()
    {
        _files.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _systemParameters
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);
        _sut = new WindowsThemeService(
            _log.Object,
            _registry.Object,
            _systemParameters.Object,
            _files.Object,
            _slideshow.Object,
            _interactiveUser.Object,
            _shell.AsFactory(),
            Mock.Of<ILocalizationService>());
    }

    private void Kind(int? backgroundType) =>
        _registry.Setup(r => r.GetValue(WallpapersKey, "BackgroundType")).Returns(backgroundType);

    private void Reads(string key, string valueName, object? value) =>
        _registry.Setup(r => r.GetValue(key, valueName)).Returns(value);

    private void RecordedAlbum() =>
        Reads(WallpapersKey, "SlideshowDirectoryPath1", WindowsThemeService.EncodeFolderId(AlbumId));

    private void VerifyLogged(LogLevel level, string fragment) =>
        _log.Verify(
            l => l.Log(level, It.Is<string>(m => m.Contains(fragment)), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);

    [Fact]
    public void A_picture_is_re_asserted_from_the_path_the_registry_holds()
    {
        Kind(0);
        Reads(DesktopKey, "WallPaper", @"D:\one.jpg");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, @"D:\one.jpg", 2), Times.Once);
    }

    [Fact]
    public void No_recorded_kind_is_treated_as_a_picture()
    {
        Kind(null);
        Reads(DesktopKey, "WallPaper", @"D:\one.jpg");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, @"D:\one.jpg", 2), Times.Once);
    }

    // Measured on a Spotlight VM (2026-09-16): WallPaper holds a REAL file under DesktopSpotlight\Assets, so the
    // picture branch would show it and write BackgroundType = 0, turning Spotlight off silently.
    [Fact]
    public void Spotlight_is_left_alone_rather_than_pinned_as_a_picture()
    {
        Kind(3);
        Reads(DesktopKey, "WallPaper",
            @"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\DesktopSpotlight\Assets\Images\image_1.jpg");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(
            s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()),
            Times.Never);
        _registry.Verify(
            r => r.SetValue(WallpapersKey, "BackgroundType", It.IsAny<object>(), It.IsAny<RegistryValueKind>()),
            Times.Never);
    }

    // Windows empties WallPaper for a colour and keeps the last picture in CurrentWallpaperPath.
    [Fact]
    public void Switching_back_from_a_colour_shows_the_last_picture_and_records_it()
    {
        Kind(0);
        Reads(DesktopKey, "WallPaper", string.Empty);
        Reads(WallpapersKey, "CurrentWallpaperPath", @"D:\one.jpg");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, @"D:\one.jpg", 2), Times.Once);
        _registry.Verify(r => r.SetValue(DesktopKey, "WallPaper", @"D:\one.jpg", RegistryValueKind.String), Times.Once);
        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, string.Empty, 2), Times.Never);
    }

    [Fact]
    public void Switching_back_from_a_slideshow_skips_the_transcoded_slide()
    {
        Kind(0);
        Reads(DesktopKey, "WallPaper", Transcoded);
        Reads(WallpapersKey, "CurrentWallpaperPath", @"D:\one.jpg");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, @"D:\one.jpg", 2), Times.Once);
    }

    [Fact]
    public void A_last_picture_that_is_gone_falls_back_to_the_latest_recent_image()
    {
        Kind(0);
        Reads(WallpapersKey, "CurrentWallpaperPath", @"D:\gone.jpg");
        Reads(WallpapersKey, "BackgroundHistoryPath0", @"D:\recent.jpg");
        _files.Setup(f => f.FileExists(@"D:\gone.jpg")).Returns(false);

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, @"D:\recent.jpg", 2), Times.Once);
    }

    [Fact]
    public void A_picture_kind_with_no_picture_anywhere_shows_nothing_and_says_so()
    {
        Kind(0);
        Reads(DesktopKey, "WallPaper", string.Empty);

        _sut.RefreshDesktop().Should().BeFalse();

        _systemParameters.Verify(
            s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()),
            Times.Never);
        VerifyLogged(LogLevel.Warning, "No picture to show");
    }

    // COLORREF is 0x00BBGGRR: 0 99 177 paints as 0xB16300, not 0x0063B1. The empty path takes the old picture off.
    [Fact]
    public void A_solid_color_paints_through_the_system_colors_and_clears_the_picture()
    {
        Kind(1);
        Reads(ColorsKey, "Background", "0 99 177");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SetSysColors(ColorBackground, 0xB16300), Times.Once);
        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, string.Empty, 2), Times.Once);
    }

    [Fact]
    public void A_colour_the_registry_cannot_answer_for_still_clears_the_picture()
    {
        Kind(1);
        Reads(ColorsKey, "Background", "not a colour");

        _sut.RefreshDesktop().Should().BeTrue();

        _systemParameters.Verify(s => s.SetSysColors(It.IsAny<int>(), It.IsAny<uint>()), Times.Never);
        _systemParameters.Verify(s => s.SystemParametersInfo(0x0014, 0, string.Empty, 2), Times.Once);
    }

    [Fact]
    public void Switching_to_a_slideshow_starts_the_album_windows_recorded()
    {
        Kind(2);
        RecordedAlbum();
        Reads(SlideshowKey, "Interval", 600000);
        Reads(SlideshowKey, "Shuffle", 1);
        Reads(DesktopKey, "WallpaperStyle", "22");
        Reads(DesktopKey, "TileWallpaper", "0");

        _sut.RefreshDesktop().Should().BeTrue();

        _slideshow.Verify(s => s.Set(Album, 5, 600000, true), Times.Once);
        _systemParameters.Verify(
            s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public void A_slideshow_with_no_recorded_album_starts_nothing_and_says_so()
    {
        Kind(2);

        _sut.RefreshDesktop().Should().BeFalse();

        _slideshow.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        VerifyLogged(LogLevel.Warning, "No slideshow album");
    }

    // Windows writes Interval when a slideshow starts, so an album that has never run one ticks at 30 minutes.
    [Fact]
    public void A_slideshow_with_no_recorded_tick_runs_at_the_windows_default()
    {
        _sut.SetSlideshow(Album).Should().BeTrue();

        _slideshow.Verify(s => s.Set(Album, 4, DefaultIntervalMs, false), Times.Once);
    }

    [Fact]
    public void The_broadcast_decides_what_the_refresh_reports()
    {
        Kind(0);
        Reads(DesktopKey, "WallPaper", @"D:\one.jpg");
        _systemParameters
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(0);

        _sut.RefreshDesktop().Should().BeFalse();

        VerifyLogged(LogLevel.Error, "SystemParametersInfo");
    }

    [Fact]
    public void Refreshing_a_picture_records_the_path_and_the_kind_windows_reads_back()
    {
        Kind(null);
        Reads(DesktopKey, "WallPaper", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg");

        _sut.RefreshDesktop().Should().BeTrue();

        _registry.Verify(
            r => r.SetValue(DesktopKey, "WallPaper", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg", RegistryValueKind.String),
            Times.Once);
        _registry.Verify(
            r => r.SetValue(WallpapersKey, "CurrentWallpaperPath", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg", RegistryValueKind.String),
            Times.Once);
        _registry.Verify(r => r.SetValue(WallpapersKey, "BackgroundType", 0, RegistryValueKind.DWord), Times.Once);
        _systemParameters.Verify(
            s => s.SystemParametersInfo(0x0014, 0, @"C:\Windows\Web\Wallpaper\Windows\img19.jpg", 2), Times.Once);
    }

    [Fact]
    public void An_album_hands_the_shell_the_folder_and_the_options_beside_it()
    {
        Reads(SlideshowKey, "Interval", 60000);
        Reads(SlideshowKey, "Shuffle", 0);
        Reads(DesktopKey, "WallpaperStyle", "0");
        Reads(DesktopKey, "TileWallpaper", "1");

        _sut.SetSlideshow(Album).Should().BeTrue();

        _slideshow.Verify(s => s.Set(Album, 1, 60000, false), Times.Once);
    }

    // The shell writes the folder id and the ini itself, so nothing here writes the registry for a slideshow.
    [Fact]
    public void An_album_writes_no_registry_value_and_broadcasts_nothing()
    {
        _sut.SetSlideshow(Album).Should().BeTrue();

        _registry.Verify(
            r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()),
            Times.Never);
        _systemParameters.Verify(
            s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()),
            Times.Never);
    }

    // The COM object acts on the profile of the process that created it: under OTS, the admin's desktop.
    [Fact]
    public void An_album_under_ots_elevation_is_refused_and_logged()
    {
        _interactiveUser.Setup(i => i.IsOtsElevation).Returns(true);
        _interactiveUser.Setup(i => i.InteractiveUserName).Returns("Marco");

        _sut.SetSlideshow(Album).Should().BeFalse();

        _slideshow.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        VerifyLogged(LogLevel.Error, "Marco");
    }

    [Fact]
    public void A_slideshow_refresh_under_ots_elevation_is_refused_too()
    {
        Kind(2);
        RecordedAlbum();
        _interactiveUser.Setup(i => i.IsOtsElevation).Returns(true);
        _interactiveUser.Setup(i => i.InteractiveUserName).Returns("Marco");

        _sut.RefreshDesktop().Should().BeFalse();

        _slideshow.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        VerifyLogged(LogLevel.Error, "Marco");
    }

    [Fact]
    public void An_album_this_pc_does_not_have_is_refused_and_logged()
    {
        _files.Setup(f => f.DirectoryExists(Album)).Returns(false);

        _sut.SetSlideshow(Album).Should().BeFalse();

        _slideshow.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        VerifyLogged(LogLevel.Error, Album);
    }

    [Fact]
    public void A_shell_that_refuses_the_album_is_reported_not_thrown()
    {
        _slideshow
            .Setup(s => s.Set(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Throws(new COMException("the shell said no"));

        _sut.SetSlideshow(Album).Should().BeFalse();

        VerifyLogged(LogLevel.Error, "the shell said no");
    }
}
