using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelListTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();
    private readonly Mock<ISettingsLoadingService> _settingsLoadingService = new();
    private readonly Mock<IEventBus> _eventBus = new();

    private readonly Dictionary<string, SettingChoice> _authored = new();

    public SettingItemViewModelListTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        _localizationService.MirrorTryGetString();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Config);
        _modeService.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(e => _authored[e.SettingId] = e);
        // A rebuilt card reads the record back through this; without it every rebuild assertion is vacuous.
        _modeService.Setup(m => m.GetBuilderEdit(It.IsAny<string>()))
            .Returns<string>(id => _authored.TryGetValue(id, out var e) ? e : null);
        _modeService.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
    }

    private static readonly Setting ListSetting =
        AutounattendCatalog.All.Single(s => s.Id == "autounattend-accounts");

    private static readonly Setting ParentSetting =
        AutounattendCatalog.All.Single(s => s.Id == ListSetting.EnabledWhen!.OtherId);

    private static int StateIndex(LocKey label) =>
        ParentSetting.States.Select((state, index) => (state, index)).First(pair => pair.state.Label == label).index;

    private static readonly int CreateTheAccountsNow = StateIndex(ListSetting.EnabledWhen!.States[0]);

    private static readonly int LocalAccountOnly = StateIndex(LocKey.Setting.AutounattendAccount.Option0);

    private static int GroupIndexFor(LocKey label) =>
        ListSetting.List!.Fields.Single(f => f.Key == "group").Options!
            .Select((option, index) => (option, index)).First(pair => pair.option == label).index;

    private static readonly int Administrators = GroupIndexFor(LocKey.AutounattendAccounts.GroupAdministrators);

    private static readonly int Users = GroupIndexFor(LocKey.AutounattendAccounts.GroupUsers);

    private SettingItemViewModel CreateCard(Setting setting, InputType inputType) =>
        new(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = setting.Display.Name.Value,
                Description = setting.Display.Description.Value,
                InputType = inputType,
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

    private SettingItemViewModel CreateSut() => CreateCard(ListSetting, InputType.List);

    private SettingItemViewModel CreateParent() => CreateCard(ParentSetting, InputType.Selection);

    // The gate lives on the feature view model, so a round trip that only drives the two cards would never close it.
    private TestableSettingsFeatureViewModel CreateFeature() =>
        new(_settingsLoadingService.Object, _logService.Object, _localizationService.Object,
            _dispatcherService.Object, _eventBus.Object, _modeService.Object);

    private void SetupLoad(params SettingItemViewModel[] cards) =>
        _settingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>(cards));

    private ChoiceValue.List Recorded() =>
        (ChoiceValue.List)_authored["autounattend-accounts"].Value;

    private static string? SignsIn(ChoiceValue.List value) =>
        value.Rows.FirstOrDefault(row => row.Values.GetValueOrDefault("auto-logon") == "true")?
            .Values.GetValueOrDefault("name");

    private static ListFieldViewModel FieldOf(ListRowViewModel row, string key) => row.FieldFor(key)!;

    [Fact]
    public void The_only_row_cannot_be_removed_until_a_second_one_exists()
    {
        var sut = CreateSut();

        sut.AddRow();
        Assert.False(Assert.Single(sut.Rows).CanRemove);

        sut.AddRow();
        Assert.All(sut.Rows, row => Assert.True(row.CanRemove));

        sut.Rows[1].Remove();
        Assert.False(Assert.Single(sut.Rows).CanRemove);
    }

    [Fact]
    public void Adding_a_row_records_nothing_until_it_has_a_name()
    {
        var sut = CreateSut();

        sut.AddRow();

        sut.Rows.Should().ContainSingle();
        Recorded().Rows.Should().BeEmpty();
    }

    [Fact]
    public void Naming_a_row_puts_it_in_the_record()
    {
        var sut = CreateSut();
        sut.AddRow();

        FieldOf(sut.Rows[0], "name").Text = "Marco";

        Recorded().Rows.Should().ContainSingle()
            .Which.Values.GetValueOrDefault("name").Should().Be("Marco");
    }

    [Fact]
    public void Every_field_the_catalog_declares_is_on_the_row()
    {
        var sut = CreateSut();

        sut.AddRow();

        sut.Rows[0].Fields.Select(field => field.Key).Should()
            .Equal(ListSetting.List!.Fields.Select(field => field.Key));
    }

    [Fact]
    public void The_second_row_taking_the_sign_in_clears_the_first()
    {
        var sut = CreateSut();
        sut.AddRow();
        sut.AddRow();
        FieldOf(sut.Rows[0], "name").Text = "First";
        FieldOf(sut.Rows[1], "name").Text = "Second";
        FieldOf(sut.Rows[0], "auto-logon").Checked = true;

        FieldOf(sut.Rows[1], "auto-logon").Checked = true;

        FieldOf(sut.Rows[0], "auto-logon").Checked.Should().BeFalse();
        SignsIn(Recorded()).Should().Be("Second");
    }

    [Fact]
    public void Removing_the_signing_in_row_records_no_automatic_sign_in()
    {
        var sut = CreateSut();
        sut.AddRow();
        var row = sut.Rows[0];
        FieldOf(row, "name").Text = "Marco";
        FieldOf(row, "auto-logon").Checked = true;
        SignsIn(Recorded()).Should().Be("Marco");

        row.Remove();

        sut.Rows.Should().BeEmpty();
        Recorded().Rows.Should().BeEmpty();
        SignsIn(Recorded()).Should().BeNull();
    }

    [Fact]
    public void A_row_in_the_users_group_cannot_sign_in()
    {
        var sut = CreateSut();
        sut.AddRow();
        var row = sut.Rows[0];
        FieldOf(row, "name").Text = "Guest";
        FieldOf(row, "group").OptionIndex = Users;

        FieldOf(row, "auto-logon").Checked = true;

        FieldOf(row, "auto-logon").Checked.Should().BeFalse(
            because: "the catalog marks the field usable only while the group field names Administrators");
        SignsIn(Recorded()).Should().BeNull();
    }

    [Fact]
    public void The_password_banner_shows_only_in_builder()
    {
        var sut = CreateSut();
        sut.ShowPasswordBanner.Should().BeTrue();

        _modeService.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
        sut.ShowPasswordBanner.Should().BeTrue();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        sut.ShowPasswordBanner.Should().BeFalse();
    }

    [Fact]
    public void Ticking_the_box_records_the_same_rows_with_the_passwords_kept()
    {
        var sut = CreateSut();
        sut.AddRow();
        FieldOf(sut.Rows[0], "name").Text = "Marco";
        FieldOf(sut.Rows[0], "password").Text = "hunter2";
        var before = Recorded();
        before.SavePasswords.Should().BeFalse();
        before.Rows.Single().Values.GetValueOrDefault("password").Should().Be("hunter2");

        sut.SavePasswords = true;

        Recorded().SavePasswords.Should().BeTrue();
        Recorded().Rows.Should().Equal(before.Rows);
    }

    [Fact]
    public void A_row_edit_after_the_box_still_records_the_box()
    {
        var sut = CreateSut();
        sut.SavePasswords = true;
        sut.AddRow();

        FieldOf(sut.Rows[0], "name").Text = "Marco";

        Recorded().SavePasswords.Should().BeTrue();
    }

    [Fact]
    public void A_password_ending_in_a_space_reaches_the_config_file_and_comes_back_whole()
    {
        var sut = CreateSut();
        sut.AddRow();
        FieldOf(sut.Rows[0], "name").Text = "Marco";
        FieldOf(sut.Rows[0], "password").Text = "hunter2 ";
        sut.SavePasswords = true;

        Recorded().Rows.Single().Values.GetValueOrDefault("password").Should().Be("hunter2 ");

        var item = new ConfigurationItem { Id = ListSetting.Id };
        ConfigFileMapper.WriteValue(item, ListSetting, Recorded());

        item.Rows!.Single()["password"].Should().Be(AccountPasswords.Obscure("hunter2 ", "Password"));
        ConfigFileMapper.DecodeValue(ListSetting, item).Should().Be(Recorded());
    }

    [Fact]
    public async Task Flipping_the_parent_away_and_back_keeps_the_rows()
    {
        var parent = CreateParent();
        var list = CreateSut();
        SetupLoad(parent, list);
        var feature = CreateFeature();
        await feature.LoadSettingsAsync();

        parent.ApplySelectionValue(CreateTheAccountsNow);
        list.EffectiveIsEnabled.Should().BeTrue();
        list.AddRow();
        FieldOf(list.Rows[0], "name").Text = "Marco";
        FieldOf(list.Rows[0], "auto-logon").Checked = true;
        list.AddRow();
        FieldOf(list.Rows[1], "name").Text = "Guest";

        parent.ApplySelectionValue(LocalAccountOnly);
        list.EffectiveIsEnabled.Should().BeFalse(because: "a greyed card is how the closed gate shows");

        parent.ApplySelectionValue(CreateTheAccountsNow);

        list.Rows.Select(row => FieldOf(row, "name").Text).Should().Equal("Marco", "Guest");
        FieldOf(list.Rows[0], "auto-logon").Checked.Should().BeTrue();
        Recorded().Rows.Select(row => row.Values.GetValueOrDefault("name")).Should().Equal("Marco", "Guest");
        SignsIn(Recorded()).Should().Be("Marco",
            because: "a card that shows the right rows and files the wrong ones is the same bug to the user");
    }

    // Opening the page again, a language change or a filter change throws every card away and builds a new one.
    [Fact]
    public async Task A_rebuild_between_the_flips_brings_the_rows_back()
    {
        var parent = CreateParent();
        var list = CreateSut();
        SetupLoad(parent, list);
        var feature = CreateFeature();
        await feature.LoadSettingsAsync();

        parent.ApplySelectionValue(CreateTheAccountsNow);
        list.AddRow();
        FieldOf(list.Rows[0], "name").Text = "Marco";
        FieldOf(list.Rows[0], "password").Text = "hunter2";
        FieldOf(list.Rows[0], "auto-logon").Checked = true;
        list.SavePasswords = true;
        parent.ApplySelectionValue(LocalAccountOnly);

        // The factory applies the overlay as the last thing it does, so a rebuilt card arrives carrying it.
        var rebuiltParent = CreateParent();
        var rebuiltList = CreateSut();
        rebuiltParent.ApplyAuthoredOverlay();
        rebuiltList.ApplyAuthoredOverlay();
        SetupLoad(rebuiltParent, rebuiltList);
        await feature.RefreshSettingsAsync();

        rebuiltParent.ApplySelectionValue(CreateTheAccountsNow);

        rebuiltList.EffectiveIsEnabled.Should().BeTrue();
        rebuiltList.Rows.Should().ContainSingle();
        FieldOf(rebuiltList.Rows[0], "name").Text.Should().Be("Marco");
        FieldOf(rebuiltList.Rows[0], "password").Text.Should().Be("hunter2");
        FieldOf(rebuiltList.Rows[0], "group").OptionIndex.Should().Be(Administrators);
        FieldOf(rebuiltList.Rows[0], "auto-logon").Checked.Should().BeTrue(
            because: "the record names the row that signs in, and the rebuilt row is that row");
        rebuiltList.SavePasswords.Should().BeTrue();
    }

    [Fact]
    public void A_row_typed_out_of_order_is_recorded_whole_once_it_has_a_name()
    {
        var sut = CreateSut();
        sut.AddRow();
        var row = sut.Rows[0];

        FieldOf(row, "password").Text = "hunter2";
        FieldOf(row, "auto-logon").Checked = true;
        Recorded().Rows.Should().BeEmpty(because: "nothing typed so far names an account");

        FieldOf(row, "name").Text = "Marco";

        Recorded().Rows.Single().Values.GetValueOrDefault("password").Should().Be("hunter2");
        SignsIn(Recorded()).Should().Be("Marco");
    }

    // The sign-in box is IsEnabled-bound to the condition, so this order cannot be typed on screen.
    [Fact]
    public void A_sign_in_ticked_under_the_wrong_group_does_not_come_back_with_it()
    {
        var sut = CreateSut();
        sut.AddRow();
        var row = sut.Rows[0];
        FieldOf(row, "name").Text = "Marco";
        FieldOf(row, "group").OptionIndex = Users;

        FieldOf(row, "auto-logon").Checked = true;
        FieldOf(row, "group").OptionIndex = Administrators;

        FieldOf(row, "auto-logon").Checked.Should().BeFalse();
        SignsIn(Recorded()).Should().BeNull();
    }

    [Fact]
    public void Moving_the_group_away_drops_a_sign_in_already_taken()
    {
        var sut = CreateSut();
        sut.AddRow();
        var row = sut.Rows[0];
        FieldOf(row, "name").Text = "Marco";
        FieldOf(row, "auto-logon").Checked = true;

        FieldOf(row, "group").OptionIndex = Users;

        FieldOf(row, "auto-logon").Checked.Should().BeFalse();
        FieldOf(row, "auto-logon").IsEnabled.Should().BeFalse();
        SignsIn(Recorded()).Should().BeNull();
    }
}
