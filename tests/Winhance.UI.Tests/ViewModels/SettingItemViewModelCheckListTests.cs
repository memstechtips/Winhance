using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelCheckListTests
{
    private static readonly string[] Editions = ["Windows 11 Home", "Windows 11 Home Single Language", "Windows 11 Pro"];

    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private readonly Dictionary<string, SettingChoice> _authored = new();

    public SettingItemViewModelCheckListTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        _localizationService.MirrorTryGetString();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
        _modeService.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(e => _authored[e.SettingId] = e);
        _modeService.Setup(m => m.GetBuilderEdit(It.IsAny<string>()))
            .Returns<string>(id => _authored.TryGetValue(id, out var e) ? e : null);
        _modeService.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
    }

    private static readonly Setting CheckListSetting =
        AutounattendCatalog.All.Single(s => s.Id == "autounattend-media-editions-list");

    private SettingItemViewModel CreateSut() =>
        new(
            new SettingItemViewModelConfig
            {
                Setting = CheckListSetting,
                SettingId = CheckListSetting.Id,
                Name = CheckListSetting.Display.Name.Value,
                Description = CheckListSetting.Display.Description.Value,
                InputType = InputType.CheckList,
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
            _modeService.Object);

    [Fact]
    public void Every_entry_starts_ticked_and_nothing_is_recorded()
    {
        var sut = CreateSut();

        sut.SetCheckListEntries(Editions);

        sut.CheckItems.Select(item => item.Key).Should().Equal(Editions);
        sut.CheckItems.Should().OnlyContain(item => item.IsChecked);
        _authored.Should().BeEmpty();
    }

    [Fact]
    public void Unticking_an_entry_records_the_ones_still_ticked_in_list_order()
    {
        var sut = CreateSut();
        sut.SetCheckListEntries(Editions);

        sut.CheckItems[1].IsChecked = false;

        _authored[CheckListSetting.Id].Value.Should().Be(new ChoiceValue.Keys(["Windows 11 Home", "Windows 11 Pro"]));
    }

    [Fact]
    public void A_reopened_record_ticks_only_its_entries_even_when_the_entries_arrive_later()
    {
        _authored[CheckListSetting.Id] = new SettingChoice(CheckListSetting.Id, new ChoiceValue.Keys(["Windows 11 Pro"]));
        var sut = CreateSut();

        sut.ApplyAuthoredOverlay();
        sut.SetCheckListEntries(Editions);

        sut.CheckItems.Where(item => item.IsChecked).Select(item => item.Key).Should().Equal("Windows 11 Pro");
    }

    [Fact]
    public void A_reopened_record_reticks_entries_already_on_the_card()
    {
        var sut = CreateSut();
        sut.SetCheckListEntries(Editions);
        _authored[CheckListSetting.Id] = new SettingChoice(CheckListSetting.Id, new ChoiceValue.Keys(["Windows 11 Home"]));

        sut.ApplyAuthoredOverlay();

        sut.CheckItems.Where(item => item.IsChecked).Select(item => item.Key).Should().Equal("Windows 11 Home");
        sut.CheckItems.Select(item => item.Key).Should().Equal(Editions);
    }
}
