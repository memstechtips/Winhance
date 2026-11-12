using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WindowsThemeServiceOptionTests
{
    private const string DesktopKey = @"HKEY_CURRENT_USER\Control Panel\Desktop";
    private const string WallpapersKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers";
    private const string ColorsKey = @"HKEY_CURRENT_USER\Control Panel\Colors";

    private const string Win11Light = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";
    private const string Win11Dark = @"C:\Windows\Web\Wallpaper\Windows\img19.jpg";
    private const string Win10 = @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg";

    // Slideshow probe of 2026-09-11 (build 26100): the id Windows wrote for D:\VMShare\Desktop 4K (3840x2160),
    // taken from slideshow.ini's ImagesRootPIDL.
    private const string ProbeId =
        "2GQVA8BAvAAE3aa9ZAwLEpDXAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQnGZ5llfPNSNe2Fzwr7oo7d"
        + "ssf9v4gFKNag+YFDox7gWBQMAAAAAAAAAAAAQAgVNNFahJXZAAEAJAABA8uvAAAAAAAAAAgLAAAAAAAAAAAAAAAAAAAAAAAA"
        + "AAAAAAAAAAAAAAgVA0EATBAaAEGAyBQZAAAAWAQCBEDAAAAAA4NXGVEEAQURTtEVP5XMAAgXAkAAEAw7+6NXoU0KdZlUuAAA"
        + "AQDIPAAAAUBAAAAAAAAAAAAAAAAAAAgWjcSAEBQZAMHArBAdA8GAwBAIAQDALBAIAgCAzAAOAQDAwAAeAIDAxAgNAADApAAA"
        + "AgBATCAAAcCAv7bhAAAAxMFUTdbnu+fjc8/QByIhApzoz1SaAAAAkBAAAAwHAAAAsAAAAcHApBgbAQGAvBwdAMHAuAQaA0GA"
        + "tBQZAIHAzBQaAYHAlBwYA8GAuBAdAIHAvBAbAAHAhBgbAUGAsBwXAMGA3BQNA4GAxAAaAIDA0BAeAkHAlBwdAkHAAAAAAAAA"
        + "AAAAAgBAAAA";

    // Three-monitor probe of 2026-09-10 (build 26100), cut after the path's terminator: six DWORDs (a tag, a size,
    // width 3840, height 2160, a FILETIME), then the UTF-16LE original path. The real value is 800 bytes, zero-padded.
    private const string PrimaryBlobHex =
        "7AC30100DA942100000F000070080000736836AF6F05DD0144003A005C0056004D0053006800610072"
        + "0065005C004400650073006B0074006F007000200034004B002000280033003800340030007800320031"
        + "003600300029005C003000330020002D0020004D0065006D006F0072007900200048006F00720069007A"
        + "006F006E00730020002D0020004C006100760065006E0064006500720020004600690065006C0064002E"
        + "006A0070006700000000000000";

    private static readonly byte[] ShellItems = [0x14, 0x00, 0x1F, 0x50, 0xE0, 0x4F];

    private readonly Mock<ILocalizationService> _localization = new();
    private readonly WindowsThemeService _sut;

    public WindowsThemeServiceOptionTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => $"loc:{key}");
        _sut = OptionProviderFixtures.ThemeService(_localization.Object);
    }

    private static Setting Picture => SettingCatalog.Find("theme-wallpaper-picture")!;

    private static Setting Color => SettingCatalog.Find("theme-wallpaper-color")!;

    // 24 header bytes the reader skips, the path, then the UTF-16 NUL it ends at.
    private static byte[] Blob(string path)
    {
        var text = Encoding.Unicode.GetBytes(path);
        var blob = new byte[24 + text.Length + 2];
        text.CopyTo(blob, 24);
        return blob;
    }

    [Fact]
    public void Options_Picture_ListsThePicturesWindowsShipsFirst()
    {
        _sut.Options(Picture, new FakeDetectionContext()).Select(o => o.Value)
            .Should().Equal(Win11Light, Win11Dark, Win10);
    }

    [Fact]
    public void Options_ShippedPicture_IsCaptionedInTheUsersLanguage()
    {
        _sut.Options(Picture, new FakeDetectionContext()).Select(o => o.Label)
            .Should().Equal(Picture.States.Select(s => $"loc:{s.Label.Value}"));
    }

    [Fact]
    public void Options_PictureThisBuildDoesNotShip_IsNotOffered()
    {
        var context = new FakeDetectionContext().Existing(Win11Light, Win11Dark);

        _sut.Options(Picture, context).Select(o => o.Value).Should().Equal(Win11Light, Win11Dark);
    }

    [Fact]
    public void Options_ThisPcsOwnPicture_IsOfferedBesideTheShippedOnes()
    {
        var context = new FakeDetectionContext()
            .Existing(Win11Light, @"D:\Pictures\lake.jpg")
            .Set(DesktopKey, "WallPaper", @"D:\Pictures\lake.jpg");

        var options = _sut.Options(Picture, context);

        options.Select(o => o.Value).Should().Equal(Win11Light, @"D:\Pictures\lake.jpg");
        options[^1].Label.Should().Be("lake.jpg");
    }

    // The shell records the path it was given, so the same picture can arrive twice spelled differently.
    [Fact]
    public void Options_PictureAlreadyListed_IsNotListedTwice()
    {
        var context = new FakeDetectionContext()
            .Existing(Win11Light)
            .Set(DesktopKey, "WallPaper", Win11Light.ToUpperInvariant())
            .Set(WallpapersKey, "BackgroundHistoryPath0", Win11Light);

        _sut.Options(Picture, context).Select(o => o.Value).Should().Equal(Win11Light);
    }

    [Fact]
    public void Options_RecentsWindowsRemembers_AreOfferedNewestFirst()
    {
        var context = new FakeDetectionContext()
            .Existing(@"D:\one.jpg", @"D:\two.jpg")
            .Set(WallpapersKey, "BackgroundHistoryPath0", @"D:\one.jpg")
            .Set(WallpapersKey, "BackgroundHistoryPath1", @"D:\two.jpg");

        _sut.Options(Picture, context).Select(o => o.Value).Should().Equal(@"D:\one.jpg", @"D:\two.jpg");
    }

    [Fact]
    public void Options_RecentPictureThisPcNoLongerHas_IsNotOffered()
    {
        var context = new FakeDetectionContext()
            .Existing(@"D:\one.jpg")
            .Set(WallpapersKey, "BackgroundHistoryPath0", @"D:\one.jpg")
            .Set(WallpapersKey, "BackgroundHistoryPath1", @"D:\gone.jpg");

        _sut.Options(Picture, context).Select(o => o.Value).Should().Equal(@"D:\one.jpg");
    }

    // BackgroundType is absent on a profile that has never left a picture behind, which still means a picture.
    [Fact]
    public void CurrentKey_BackgroundIsAPictureOrUnrecorded_ReadsTheCurrentPicture()
    {
        var absent = new FakeDetectionContext().Set(DesktopKey, "WallPaper", @"D:\one.jpg");
        var picture = new FakeDetectionContext()
            .Set(WallpapersKey, "BackgroundType", 0)
            .Set(DesktopKey, "WallPaper", @"D:\one.jpg");

        _sut.CurrentKey(Picture, absent).Should().Be(@"D:\one.jpg");
        _sut.CurrentKey(Picture, picture).Should().Be(@"D:\one.jpg");
    }

    [Fact]
    public void CurrentKey_ThreeMonitors_ReadsAsOnePicture()
    {
        var context = new FakeDetectionContext()
            .Set(DesktopKey, "TranscodedImageCount", 3)
            .Set(DesktopKey, "TranscodedImageCache_000", Blob(@"D:\one.jpg"))
            .Set(DesktopKey, "TranscodedImageCache_001", Blob(@"D:\two.jpg"))
            .Set(DesktopKey, "TranscodedImageCache_002", Blob(@"D:\three.jpg"));

        _sut.CurrentKey(Picture, context).Should().Be(@"D:\one.jpg");
    }

    [Fact]
    public void CurrentKey_OneMonitor_ReadsTheUnsuffixedBlob()
    {
        var context = new FakeDetectionContext()
            .Set(DesktopKey, "TranscodedImageCount", 1)
            .Set(DesktopKey, "TranscodedImageCache", Blob(@"D:\only.jpg"));

        _sut.CurrentKey(Picture, context).Should().Be(@"D:\only.jpg");
    }

    [Fact]
    public void CurrentKey_TranscodedCopy_IsNotAPicture()
    {
        var context = new FakeDetectionContext()
            .Set(DesktopKey, "WallPaper", @"C:\Users\x\AppData\Roaming\Microsoft\Windows\Themes\TranscodedWallpaper");

        _sut.CurrentKey(Picture, context).Should().BeNull();
    }

    [Fact]
    public void Options_ThisPcsPicture_IsOfferedAsTheOriginalTheCacheNames()
    {
        var context = new FakeDetectionContext()
            .Existing(@"D:\Pictures\lake.jpg")
            .Set(DesktopKey, "TranscodedImageCache", Blob(@"D:\Pictures\lake.jpg"))
            .Set(DesktopKey, "WallPaper", @"C:\Users\x\AppData\Roaming\Microsoft\Windows\Themes\TranscodedWallpaper");

        _sut.Options(Picture, context).Select(o => o.Value).Should().Equal(@"D:\Pictures\lake.jpg");
    }

    [Fact]
    public void CurrentKey_SolidColourOrSlideshow_IsNoPictureSelection()
    {
        var colour = new FakeDetectionContext()
            .Set(WallpapersKey, "BackgroundType", 1)
            .Set(DesktopKey, "WallPaper", @"D:\one.jpg");

        _sut.CurrentKey(Picture, colour).Should().BeNull();
    }

    [Fact]
    public void CurrentKey_EmptyWallpaperValue_IsNoPictureSelection()
    {
        var context = new FakeDetectionContext().Set(DesktopKey, "WallPaper", string.Empty);

        _sut.CurrentKey(Picture, context).Should().BeNull();
    }

    [Fact]
    public void SetFor_PicturePath_IsAKeyTheServiceWillWrite()
    {
        var set = KeyedOptions.SetFor(Picture, @"D:\anything.jpg", _sut);

        set!["WallPaper"].WritePayload.Should().Be(@"D:\anything.jpg");
        set["CurrentWallpaperPath"].WritePayload.Should().Be(@"D:\anything.jpg");
        KeyedOptions.SetFor(Picture, string.Empty, _sut).Should().BeNull();
    }

    [Fact]
    public void Accepts_AnythingThatDoesNotNameAPicture_IsNoKey()
    {
        _sut.Accepts(Picture, @"D:\Pictures\lake.jpg")
            .Should().BeTrue("a config from another machine can name a picture this PC never had");
        _sut.Accepts(Picture, @"C:\Users\me\secret.kdbx").Should().BeFalse();
    }

    [Fact]
    public void Travels_ShippedPictureVersusAnythingElse_TravelsAsPathVersusBytes()
    {
        _sut.Travels(Win11Light).Should().BeFalse();
        _sut.Travels(@"D:\Pictures\lake.jpg").Should().BeTrue();
        _sut.DestinationFor(@"D:\Pictures\lake.jpg").Should().Be(@"C:\Windows\Web\Wallpaper\Winhance\lake.jpg");
    }

    [Fact]
    public void Options_Color_IsThePaletteOfTwentyFourWindowsOffers()
    {
        var swatches = _sut.Options(Color, new FakeDetectionContext());

        swatches.Should().HaveCount(24);
        swatches[0].Value.Should().Be("#FF8C00");
        swatches[23].Value.Should().Be("#000000");
    }

    [Fact]
    public void Options_Swatch_IsCaptionedByItsOwnHex()
    {
        var swatch = _sut.Options(Color, new FakeDetectionContext())[0];

        swatch.Label.Should().Be("#FF8C00");
        swatch.Value.Should().Be("#FF8C00");
    }

    [Fact]
    public void Options_ColourNotInThePalette_IsOfferedBesideIt()
    {
        var context = new FakeDetectionContext().Set(ColorsKey, "Background", "1 2 3");

        var options = _sut.Options(Color, context);

        options.Should().HaveCount(25);
        options[24].Value.Should().Be("#010203");
    }

    [Fact]
    public void Options_ColourAlreadyInThePalette_IsNotOfferedTwice()
    {
        var context = new FakeDetectionContext().Set(ColorsKey, "Background", "255 140 0");

        _sut.Options(Color, context).Should().HaveCount(24);
    }

    [Fact]
    public void CurrentKey_Color_IsReadOnlyWhileTheBackgroundIsASolidColour()
    {
        var colour = new FakeDetectionContext()
            .Set(WallpapersKey, "BackgroundType", 1)
            .Set(ColorsKey, "Background", "255 140 0");
        var picture = new FakeDetectionContext()
            .Set(WallpapersKey, "BackgroundType", 0)
            .Set(ColorsKey, "Background", "255 140 0");

        _sut.CurrentKey(Color, colour).Should().Be("#FF8C00");
        _sut.CurrentKey(Color, picture).Should().BeNull();
    }

    [Fact]
    public void CurrentKey_UnreadableBackgroundValue_IsNoSelection()
    {
        var context = new FakeDetectionContext()
            .Set(WallpapersKey, "BackgroundType", 1)
            .Set(ColorsKey, "Background", "not a colour");

        _sut.CurrentKey(Color, context).Should().BeNull();
    }

    [Fact]
    public void SetFor_PickedColour_IsWrittenAsTheThreeChannelsWindowsStores()
    {
        KeyedOptions.SetFor(Color, "#010203", _sut)!["Background"].WritePayload.Should().Be("1 2 3");
        KeyedOptions.SetFor(Color, "nope", _sut).Should().BeNull();
    }

    // The colour the probe read out of Control Panel\Colors\Background on 2026-09-11.
    [Fact]
    public void HexFromRegistryColor_ThreeChannels_RoundTripsWithRegistryColorFromHex()
    {
        var hex = WindowsThemeService.HexFromRegistryColor("0 99 177");

        hex.Should().Be("#0063B1");
        WindowsThemeService.RegistryColorFromHex(hex).Should().Be("0 99 177");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0 99")]
    [InlineData("0 99 177 255")]
    [InlineData("0 99 300")]
    [InlineData("blue")]
    public void HexFromRegistryColor_ValueThatIsNotThreeChannels_ReturnsNull(string? stored)
    {
        WindowsThemeService.HexFromRegistryColor(stored).Should().BeNull();
    }

    [Fact]
    public void RegistryColorFromHex_WithOrWithoutTheHash_ReadsTheSame()
    {
        WindowsThemeService.RegistryColorFromHex("#FF8C00").Should().Be("255 140 0");
        WindowsThemeService.RegistryColorFromHex("ff8c00").Should().Be("255 140 0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#FF8C0")]
    [InlineData("#FF8C000")]
    [InlineData("#GGGGGG")]
    public void RegistryColorFromHex_NotSixHexDigits_ReturnsNull(string? text)
    {
        WindowsThemeService.RegistryColorFromHex(text).Should().BeNull();
    }

    [Fact]
    public void RegistryColorFromHex_ConfigFileForm_SurvivesTheRoundTrip()
    {
        WindowsThemeService.RegistryColorFromHex(WindowsThemeService.HexFromRegistryColor("72 104 96"))
            .Should().Be("72 104 96");
        WindowsThemeService.HexFromRegistryColor(WindowsThemeService.RegistryColorFromHex("#486860"))
            .Should().Be("#486860");
    }

    [Fact]
    public void DecodeFolderId_ProbeId_DecodesToTheItemListWindowsWrote()
    {
        var itemList = WindowsThemeService.DecodeFolderId(ProbeId);

        itemList.Length.Should().Be(440);
        // The name sits inside a shell item at no fixed offset, so it is looked for as a run of bytes.
        itemList.AsSpan().IndexOf(Encoding.Unicode.GetBytes("Desktop 4K (3840x2160)")).Should().BePositive();
    }

    [Fact]
    public void EncodeFolderId_ProbeId_ComesBackOutOfARoundTrip()
    {
        WindowsThemeService.EncodeFolderId(WindowsThemeService.DecodeFolderId(ProbeId)).Should().Be(ProbeId);
    }

    // The last group carries four bits of the last byte and two that nothing reads; "Q" sets one of those.
    [Fact]
    public void DecodeFolderId_BitsPastTheLastByte_DoNotSurviveARoundTrip()
    {
        var padded = string.Concat(ProbeId.AsSpan(0, ProbeId.Length - 1), "Q");

        WindowsThemeService.DecodeFolderId(padded).Should().Equal(WindowsThemeService.DecodeFolderId(ProbeId));
        WindowsThemeService.EncodeFolderId(WindowsThemeService.DecodeFolderId(padded)).Should().Be(ProbeId);
    }

    // Standard base64 reads "BA" as 0x04.
    [Fact]
    public void DecodeFolderId_GroupOfSixBits_IsReadLowBitFirst()
    {
        WindowsThemeService.DecodeFolderId("BA").Should().Equal(new byte[] { 0x01 });
    }

    [Fact]
    public void EncodeFolderId_AnyItemList_ComesBackOutOfARoundTrip()
    {
        var itemList = new byte[] { 0x14, 0x00, 0x1F, 0x50, 0xE0, 0x4F, 0xD0, 0x20, 0x00, 0x00 };

        WindowsThemeService.DecodeFolderId(WindowsThemeService.EncodeFolderId(itemList)).Should().Equal(itemList);
    }

    [Fact]
    public void DecodeFolderId_CharacterTheCodecDoesNotUse_Throws()
    {
        var act = () => WindowsThemeService.DecodeFolderId("AA*AA");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void AlbumPath_RecordedId_DecodesToTheFolderTheShellResolves()
    {
        var context = new FakeDetectionContext().Folder(ShellItems, @"D:\Albums\Trip");

        WindowsThemeService.AlbumPath(WindowsThemeService.EncodeFolderId(ShellItems), context).Should().Be(@"D:\Albums\Trip");
    }

    [Fact]
    public void AlbumPath_IdTheShellCannotResolve_ReturnsNull()
    {
        WindowsThemeService.AlbumPath("BA", new FakeDetectionContext()).Should().BeNull();
    }

    [Fact]
    public void AlbumPath_IdTheCodecCannotRead_ReturnsNullRatherThanThrowing()
    {
        WindowsThemeService.AlbumPath("AA*AA", new FakeDetectionContext()).Should().BeNull();
    }

    [Fact]
    public void AlbumPath_NoRecordedId_ReturnsNull()
    {
        var context = new FakeDetectionContext();

        WindowsThemeService.AlbumPath(null, context).Should().BeNull();
        WindowsThemeService.AlbumPath(string.Empty, context).Should().BeNull();
    }

    // IDesktopWallpaper.SetPosition numbers positions its own way; the registry records the same fact as the pair
    // WallpaperStyle + TileWallpaper. Tile wins whatever the style, and an unwritten pair reads as Fill (fresh install).
    [Theory]
    [InlineData("0", "1", 1)]
    [InlineData("10", "1", 1)]
    [InlineData("0", "0", 0)]
    [InlineData("2", "0", 2)]
    [InlineData("6", "0", 3)]
    [InlineData("10", "0", 4)]
    [InlineData("22", "0", 5)]
    [InlineData(null, null, 4)]
    public void WallpaperPosition_StyleAndTilePair_IsThePositionTheShellTakes(string? style, string? tile, int position)
    {
        WindowsThemeService.WallpaperPosition(style, tile).Should().Be(position);
    }

    [Fact]
    public void PathFromCache_ProbePrimaryBlob_ReadsTheOriginalPath()
    {
        WindowsThemeService.PathFromCache(Convert.FromHexString(PrimaryBlobHex))
            .Should().Be(@"D:\VMShare\Desktop 4K (3840x2160)\03 - Memory Horizons - Lavender Field.jpg");
    }

    [Fact]
    public void PathFromCache_BlobWithNothingAfterTheHeader_ReturnsNull()
    {
        WindowsThemeService.PathFromCache(new byte[24]).Should().BeNull();
        WindowsThemeService.PathFromCache(null).Should().BeNull();
    }

    [Fact]
    public void DestinationFor_Picture_KeepsTheFileNameUnderTheWinhanceFolder()
    {
        _sut.DestinationFor(@"D:\x\03 - a.jpg").Should().Be(@"C:\Windows\Web\Wallpaper\Winhance\03 - a.jpg");
    }

    [Fact]
    public void AlbumDestinationFor_Album_KeepsItsFolderNameUnderTheWinhanceFolder()
    {
        _sut.AlbumDestinationFor(@"D:\Pictures\Trip 2026\").Should().Be(@"C:\Windows\Web\Wallpaper\Winhance\Trip 2026");
    }

    [Fact]
    public void Travels_ShippedPicture_IsOnTheNewInstallAlready()
    {
        _sut.Travels(@"c:\windows\web\wallpaper\windows\img0.jpg").Should().BeFalse();
        _sut.Travels(@"D:\Pictures\mine.jpg").Should().BeTrue();
    }

    [Fact]
    public void Travels_PictureAnEarlierImportCarried_TravelsAgainToTheSamePlace()
    {
        const string carried = @"C:\Windows\Web\Wallpaper\Winhance\lake.jpg";

        _sut.Travels(carried).Should().BeTrue();
        _sut.DestinationFor(carried).Should().Be(carried);
    }

    [Fact]
    public void Travels_PictureUnderWindowsFolderTheSettingDoesNotShip_IsTrue()
    {
        _sut.Travels(@"C:\Windows\Web\Wallpaper\Dell\img0.jpg").Should().BeTrue();
    }

    [Fact]
    public void Travels_EveryPictureTheSettingShips_IsFalse()
    {
        Picture.States.Select(state => KeyedOptions.KeyOf(Picture, state)!)
            .Should().OnlyContain(picture => !_sut.Travels(picture));
    }

    [Theory]
    [InlineData(@"\\evil.example\share\lake.jpg")]
    [InlineData("//evil.example/share/lake.jpg")]
    [InlineData(@"lake.jpg")]
    public void Accepts_UncPicturePath_IsNoKey(string key)
        => _sut.Accepts(Picture, key).Should().BeFalse();
}
