using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class FileStoreTests
{
    private const string SettingId = "theme-wallpaper-picture";
    private const string Source = @"D:\pictures\mine.jpg";
    private const string Destination = @"C:\Windows\Web\Wallpaper\Winhance\mine.jpg";
    private const string WindowsOwn = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";

    private static readonly byte[] Bytes = [1, 2, 3];

    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILogService> _log = new();
    private readonly FileStore _sut;

    public FileStoreTests()
    {
        _files.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _files.Setup(f => f.GetFileSize(It.IsAny<string>())).Returns(Bytes.Length);
        _files
            .Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Bytes);
        _sut = new FileStore(_files.Object, _log.Object, OptionProviderFixtures.ThemeService(Mock.Of<ILocalizationService>()));
    }

    private static SelectionSet SetWith(string? base64, string key = Source) =>
        SelectionSet.Empty with
        {
            Settings = [new SettingChoice(SettingId, new ChoiceValue.Keyed(key, "mine.jpg"))],
            Files = base64 is null ? [] : [new CarriedFile(SettingId, Destination, base64)],
        };

    private static SelectionSet Carrying(string settingId, string? destination) =>
        SelectionSet.Empty with
        {
            Settings = [new SettingChoice(settingId, new ChoiceValue.Keyed(Source, "mine.jpg"))],
            Files = [new CarriedFile(settingId, destination!, Convert.ToBase64String(Bytes))],
        };

    private static ChoiceValue.Keyed KeyOf(SelectionSet set) =>
        (ChoiceValue.Keyed)set.Settings.Single(c => c.SettingId == SettingId).Value;

    private static SelectionSet Saving(string settingId, ChoiceValue value) =>
        new([new SettingChoice(settingId, value)], [], []);

    [Fact]
    public async Task LoadAsync_UsersOwnPicture_TravelsAsBytes()
    {
        var result = await _sut.LoadAsync(Saving(SettingId, new ChoiceValue.Keyed(Source, "mine.jpg")));

        var carried = result.Files.Should().ContainSingle().Subject;
        carried.SettingId.Should().Be(SettingId);
        carried.Base64.Should().Be(Convert.ToBase64String(Bytes));
        carried.Destination.Should().Be(Destination);
    }

    [Fact]
    public async Task LoadAsync_CarriedPicture_RepointsTheChoiceAtWhereTheBytesWillLand()
    {
        var result = await _sut.LoadAsync(Saving(SettingId, new ChoiceValue.Keyed(Source, "mine.jpg")));

        KeyOf(result).Key.Should().Be(Destination,
            because: "the key is a path, and on the target machine it is the path the bytes land on");
    }

    [Fact]
    public async Task LoadAsync_PictureTheNewInstallAlreadyHas_IsNeverRead()
    {
        var set = Saving(SettingId, new ChoiceValue.Keyed(WindowsOwn, "Windows 11 light"));

        var result = await _sut.LoadAsync(set);

        result.Files.Should().BeEmpty();
        KeyOf(result).Key.Should().Be(WindowsOwn);
        _files.Verify(
            f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_KeyThatNamesNoPicture_IsNeverRead()
    {
        var set = Saving(SettingId, new ChoiceValue.Keyed(@"C:\Users\me\secret.kdbx", "secret.kdbx"));

        var result = await _sut.LoadAsync(set);

        result.Should().BeSameAs(set);
        result.Files.Should().BeEmpty();
        _files.Verify(
            f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_PictureThisPcDoesNotHave_TravelsAsAPath()
    {
        _files.Setup(f => f.FileExists(Source)).Returns(false);

        var result = await _sut.LoadAsync(Saving(SettingId, new ChoiceValue.Keyed(Source, "mine.jpg")));

        result.Files.Should().BeEmpty();
        KeyOf(result).Key.Should().Be(Source,
            because: "it is still the correct choice on a PC that has the file");
    }

    [Fact]
    public async Task LoadAsync_ReadThatThrows_LeavesTheChoiceAlone()
    {
        _files
            .Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("locked"));

        var result = await _sut.LoadAsync(Saving(SettingId, new ChoiceValue.Keyed(Source, "mine.jpg")));

        result.Files.Should().BeEmpty();
        KeyOf(result).Key.Should().Be(Source);
    }

    [Fact]
    public async Task LoadAsync_KeyedChoiceWhoseListCarriesNoFile_IsUntouched()
    {
        var set = Saving("power-plan-selection", new ChoiceValue.Keyed("guid", "Balanced"));

        var result = await _sut.LoadAsync(set);

        result.Should().BeSameAs(set);
        result.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_ValueThatIsNotAKey_IsUntouched()
    {
        var set = Saving(SettingId, new ChoiceValue.Option(0));

        var result = await _sut.LoadAsync(set);

        result.Should().BeSameAs(set);
    }

    [Fact]
    public async Task MaterializeAsync_EmbeddedBytes_LandInTheWinhanceFolderAndTheChoiceNamesThem()
    {
        var result = await _sut.MaterializeAsync(SetWith(Convert.ToBase64String(Bytes)));

        _files.Verify(f => f.CreateDirectory(@"C:\Windows\Web\Wallpaper\Winhance"), Times.Once);
        _files.Verify(
            f => f.WriteAllBytesAsync(Destination, It.Is<byte[]>(b => b.SequenceEqual(Bytes)), It.IsAny<CancellationToken>()),
            Times.Once);
        KeyOf(result).Key.Should().Be(Destination);
        KeyOf(result).Label.Should().Be("mine.jpg", "only the path this PC has changes, never what the file called it");
    }

    [Fact]
    public async Task MaterializeAsync_FileThatOnlyTravelledAsAPath_PassesThroughUntouched()
    {
        var result = await _sut.MaterializeAsync(SetWith(base64: null));

        KeyOf(result).Key.Should().Be(Source);
        _files.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        _files.Verify(
            f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MaterializeAsync_BytesThatDoNotDecode_KeepTheKeyTheFileNamed()
    {
        var result = await _sut.MaterializeAsync(SetWith("not base64!!"));

        KeyOf(result).Key.Should().Be(Source);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains(SettingId)), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
        _files.Verify(
            f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MaterializeAsync_FileOnOneChoice_RewritesOnlyThatChoice()
    {
        var set = SelectionSet.Empty with
        {
            Settings =
            [
                new SettingChoice(SettingId, new ChoiceValue.Keyed(Source, "mine.jpg")),
                new SettingChoice("theme-wallpaper-color", new ChoiceValue.Keyed("#FF8C00", "#FF8C00")),
            ],
            Files = [new CarriedFile(SettingId, Destination, Convert.ToBase64String(Bytes))],
        };

        var result = await _sut.MaterializeAsync(set);

        KeyOf(result).Key.Should().Be(Destination);
        ((ChoiceValue.Keyed)result.Settings.Single(c => c.SettingId == "theme-wallpaper-color").Value)
            .Key.Should().Be("#FF8C00");
    }

    [Theory]
    [InlineData(@"C:\Users\Public\Desktop\mine.jpg")]
    [InlineData(@"..\..\..\System32\mine.jpg")]
    [InlineData("C:/Windows/System32/mine.jpg")]
    [InlineData(@"\\elsewhere\share\mine.jpg")]
    [InlineData("C:mine.jpg")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\Trip 2026\mine.jpg")]
    public async Task MaterializeAsync_CarriedFileName_IsTheOnlyNameBelieved(string named)
    {
        var result = await _sut.MaterializeAsync(Carrying(SettingId, named));

        _files.Verify(f => f.CreateDirectory(@"C:\Windows\Web\Wallpaper\Winhance"), Times.Once);
        _files.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Once);
        _files.Verify(
            f => f.WriteAllBytesAsync(Destination, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _files.Verify(
            f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        KeyOf(result).Key.Should().Be(Destination);
    }

    [Theory]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\run.exe")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\desktop.ini")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\mine.jpg:run.exe")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\run.exe:mine.jpg")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\..")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\NUL.jpg")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\COM1.png")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\con.jpg")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\mine.jpg.")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance\mine.jpg ")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_name_that_is_not_a_picture_is_never_written(string? named)
    {
        var result = await _sut.MaterializeAsync(Carrying(SettingId, named));

        KeyOf(result).Key.Should().Be(Source);
        _files.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        _files.Verify(
            f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains(SettingId)), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }

    [Theory]
    [InlineData("theme-wallpaper-color")]
    [InlineData("theme-wallpaper-album")]
    [InlineData("no-such-setting")]
    public async Task MaterializeAsync_FileOnSettingThatTakesNoPicture_IsNeverWritten(string settingId)
    {
        var set = Carrying(settingId, Destination);

        var result = await _sut.MaterializeAsync(set);

        result.Should().BeSameAs(set);
        _files.Verify(
            f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains(settingId)), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, true)]
    [InlineData(new byte[] { 1, 2, 4 }, false)]
    [InlineData(new byte[] { 1, 2 }, false)]
    public async Task IsSameFileAsync_CarriedFile_IsTheSameOnlyByteForByte(byte[] carried, bool expected)
    {
        (await _sut.IsSameFileAsync(Source, Convert.ToBase64String(carried))).Should().Be(expected);
    }

    [Fact]
    public async Task IsSameFileAsync_MissingFileOrBadBase64_IsNeverTheSame()
    {
        (await _sut.IsSameFileAsync(Source, "not base64!")).Should().BeFalse();

        _files.Setup(f => f.FileExists(Source)).Returns(false);
        (await _sut.IsSameFileAsync(Source, Convert.ToBase64String(Bytes))).Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeAsync_FileThatCannotBeWritten_IsLoggedAndTheRestStillLand()
    {
        const string otherDestination = @"C:\Windows\Web\Wallpaper\Winhance\day1.jpg";
        _files
            .Setup(f => f.WriteAllBytesAsync(Destination, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("the file is in use"));
        var set = SelectionSet.Empty with
        {
            Settings = [new SettingChoice(SettingId, new ChoiceValue.Keyed(Source, "mine.jpg"))],
            Files =
            [
                new CarriedFile(SettingId, Destination, Convert.ToBase64String(Bytes)),
                new CarriedFile(SettingId, otherDestination, Convert.ToBase64String(Bytes)),
            ],
        };

        var result = await _sut.MaterializeAsync(set);

        KeyOf(result).Key.Should().Be(otherDestination);
        _log.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(m => m.Contains(SettingId)), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }
}
