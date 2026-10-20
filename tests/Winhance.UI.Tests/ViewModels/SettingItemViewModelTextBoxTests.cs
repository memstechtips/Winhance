using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Catalogs;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelTextBoxTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private readonly Dictionary<string, SettingChoice> _authored = new();

    public SettingItemViewModelTextBoxTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        _localizationService.MirrorTryGetString();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(e => _authored[e.SettingId] = e);
        _modeService.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
    }

    private static readonly Setting ProductKey =
        AutounattendCatalog.All.Single(s => s.Id == "autounattend-product-key");

    private static readonly Setting ComputerName =
        AutounattendCatalog.All.Single(s => s.Id == "autounattend-computer-name");

    private static readonly Setting SlideshowAlbum =
        WindowsThemeCustomizationsCatalog.All.Single(s => s.Id == "theme-wallpaper-album");

    private SettingItemViewModel CreateSut(Setting setting, IFilePickerService? picker = null) =>
        new(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = setting.Display.Name.Value,
                Description = setting.Display.Description.Value,
                InputType = InputType.TextBox,
            },
            SettingWriteStrategies.Selector(
                _applyService.Object, _dialogService.Object, _localizationService.Object,
                _logService.Object, _modeService.Object),
            _logService.Object,
            _dispatcherService.Object,
            _dialogService.Object,
            _localizationService.Object,
            null,
            null,
            null,
            _modeService.Object,
            picker);

    private ChoiceValue Recorded(string settingId) => _authored[settingId].Value;

    [Fact]
    public void A_value_the_rule_refuses_is_shown_and_recorded_nowhere()
    {
        var sut = CreateSut(ProductKey);

        sut.OnTextBoxChanged("not-a-key");

        sut.TextValue.Should().Be("not-a-key");
        sut.HasTextError.Should().BeTrue();
        _authored.Should().NotContainKey("autounattend-product-key");
    }

    [Fact]
    public void A_value_the_rule_accepts_is_recorded_normalized()
    {
        var sut = CreateSut(ProductKey);

        sut.OnTextBoxChanged("vk7jg-nphtm-c97jm-9mpgt-3v66t");

        sut.HasTextError.Should().BeFalse();
        sut.TextValue.Should().Be("vk7jg-nphtm-c97jm-9mpgt-3v66t",
            because: "the box keeps what was typed; only the record is normalized");
        Recorded("autounattend-product-key").Should().BeOfType<ChoiceValue.Text>()
            .Which.Value.Should().Be("VK7JG-NPHTM-C97JM-9MPGT-3V66T");
    }

    [Fact]
    public void Clearing_the_box_records_an_empty_value()
    {
        var sut = CreateSut(ComputerName);
        sut.OnTextBoxChanged("MYPC");

        sut.OnTextBoxChanged("");

        sut.HasTextError.Should().BeFalse();
        Recorded("autounattend-computer-name").Should().BeOfType<ChoiceValue.Text>()
            .Which.Value.Should().BeEmpty();
    }

    // A typed album path would apply at every parent folder on the way, so the box takes only what Browse returns.
    [Fact]
    public void A_box_that_names_a_picker_is_read_only()
    {
        var album = CreateSut(SlideshowAlbum);

        album.TextBoxPicker.Should().Be(PickerKind.Folder);
        album.HasTextBoxPicker.Should().BeTrue();
        CreateSut(ComputerName).HasTextBoxPicker.Should().BeFalse(
            because: "a computer name is typed, so read-only would leave it unfillable");
    }

    [Fact]
    public void The_same_value_twice_is_recorded_once()
    {
        var sut = CreateSut(ComputerName);

        sut.OnTextBoxChanged("MYPC");
        sut.OnTextBoxChanged("MYPC");

        _modeService.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Once);
    }

    [Fact]
    public void A_keystroke_that_normalizes_onto_the_recorded_value_records_nothing_new()
    {
        var sut = CreateSut(ProductKey);
        sut.OnTextBoxChanged("VK7JG-NPHTM-C97JM-9MPGT-3V66T");

        sut.OnTextBoxChanged("vk7jg-nphtm-c97jm-9mpgt-3v66t");

        sut.TextValue.Should().Be("vk7jg-nphtm-c97jm-9mpgt-3v66t");
        _modeService.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Once);
    }

    // A pick is deliberate, and picking the album the box already shows is how a slideshow is started again.
    [Fact]
    public void Browsing_to_the_folder_the_box_already_shows_still_records_it()
    {
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolder(It.IsAny<string?>())).Returns(@"D:\Pictures\Holiday");
        var album = CreateSut(SlideshowAlbum, picker.Object);
        album.SeedText(@"D:\Pictures\Holiday");

        album.BrowseForText();

        Recorded("theme-wallpaper-album").Should().BeOfType<ChoiceValue.Text>()
            .Which.Value.Should().Be(@"D:\Pictures\Holiday");
    }

    [Fact]
    public void A_cancelled_browse_records_nothing()
    {
        var picker = new Mock<IFilePickerService>();
        var album = CreateSut(SlideshowAlbum, picker.Object);
        album.SeedText(@"D:\Pictures\Holiday");

        album.BrowseForText();

        _authored.Should().NotContainKey("theme-wallpaper-album");
    }
}
