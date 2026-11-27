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
    private readonly Mock<IFilePickerService> _filePicker = new();

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

    private static readonly Setting OwnInstallersSetting =
        AutounattendCatalog.All.Single(s => s.Id == "autounattend-own-installers");

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
            _modeService.Object,
            _filePicker.Object);

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

    private ChoiceValue.List RecordedInstallers() =>
        (ChoiceValue.List)_authored["autounattend-own-installers"].Value;

    private void ArrangePicked(string path) =>
        _filePicker.Setup(p => p.PickFile(It.IsAny<string[]>(), It.IsAny<string?>())).Returns(path);

    private static string? SignsIn(ChoiceValue.List value) =>
        value.Rows.FirstOrDefault(row => row.Values.GetValueOrDefault("auto-logon") == "true")?
            .Values.GetValueOrDefault("name");

    private static ListFieldViewModel FieldOf(ListRowViewModel row, string key) => row.FieldFor(key)!;

    [Fact]
    public void CanRemove_OnlyRow_IsFalseUntilASecondRowExists()
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
    public void AddRow_FileFieldPickerCancelled_AddsNoRow()
    {
        var sut = CreateCard(OwnInstallersSetting, InputType.List);

        sut.AddRow();

        Assert.Empty(sut.Rows);
        Assert.DoesNotContain("autounattend-own-installers", _authored.Keys);
    }

    [Fact]
    public void AddRow_FileField_OpensPickerWithCatalogFilter()
    {
        ArrangePicked(@"C:\Setup\Reader.exe");
        var sut = CreateCard(OwnInstallersSetting, InputType.List);
        sut.ListAddLabel = "Add an installer";

        sut.AddRow();

        _filePicker.Verify(p => p.PickFile(
            It.Is<string[]>(f => f.Length == 2 && f[0] == LocKey.AutounattendOwnInstallers.Filter.Value && f[1] == "*.exe;*.msi"),
            "Add an installer"), Times.Once);
    }

    // The row's first box is the picked file, so a row that leaves its other boxes empty is still a filled row.
    [Fact]
    public void AddRow_PickedFile_RecordsItAtOnceWithOtherFieldsEmpty()
    {
        ArrangePicked(@"C:\Setup\Reader.exe");
        var sut = CreateCard(OwnInstallersSetting, InputType.List);

        sut.AddRow();

        var row = Assert.Single(RecordedInstallers().Rows);
        Assert.Equal(@"C:\Setup\Reader.exe", row.Values["file"]);
        Assert.Equal(string.Empty, row.Values["switches"]);
        Assert.Equal(@"C:\Setup\Reader.exe", FieldOf(Assert.Single(sut.Rows), "file").Text);
    }

    [Fact]
    public void FieldText_TypedBesidePickedFile_IsRecordedOnItsRow()
    {
        ArrangePicked(@"C:\Setup\Reader.exe");
        var sut = CreateCard(OwnInstallersSetting, InputType.List);
        sut.AddRow();

        FieldOf(sut.Rows[0], "switches").Text = " /sAll ";

        var row = Assert.Single(RecordedInstallers().Rows);
        Assert.Equal(@"C:\Setup\Reader.exe", row.Values["file"]);
        Assert.Equal("/sAll", row.Values["switches"]);
    }

    [Fact]
    public void Remove_OnlyRowOfListThatCanBeEmpty_RemovesIt()
    {
        ArrangePicked(@"C:\Setup\Reader.exe");
        var sut = CreateCard(OwnInstallersSetting, InputType.List);
        sut.AddRow();
        var row = Assert.Single(sut.Rows);
        Assert.True(row.CanRemove);

        row.Remove();

        Assert.Empty(sut.Rows);
        Assert.Empty(RecordedInstallers().Rows);
    }

    [Fact]
    public void AddRow_WithoutName_RecordsNothing()
    {
        var sut = CreateSut();

        sut.AddRow();

        sut.Rows.Should().ContainSingle();
        Recorded().Rows.Should().BeEmpty();
    }

    [Fact]
    public void FieldText_NamingARow_PutsItInTheRecord()
    {
        var sut = CreateSut();
        sut.AddRow();

        FieldOf(sut.Rows[0], "name").Text = "Marco";

        Recorded().Rows.Should().ContainSingle()
            .Which.Values.GetValueOrDefault("name").Should().Be("Marco");
    }

    [Fact]
    public void AddRow_Always_PutsEveryCatalogFieldOnTheRow()
    {
        var sut = CreateSut();

        sut.AddRow();

        sut.Rows[0].Fields.Select(field => field.Key).Should()
            .Equal(ListSetting.List!.Fields.Select(field => field.Key));
    }

    [Fact]
    public void AutoLogonChecked_SecondRowTakingIt_ClearsTheFirst()
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
    public void Remove_SigningInRow_RecordsNoAutomaticSignIn()
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
    public void AutoLogonChecked_RowInUsersGroup_CannotSignIn()
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
    public void ShowPasswordBanner_Always_ShowsOnlyWhileConfigFileIsBeingBuilt()
    {
        var sut = CreateSut();
        sut.ShowPasswordBanner.Should().BeTrue();

        _modeService.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
        sut.ShowPasswordBanner.Should().BeFalse(
            because: "an answer file carries every password whether or not the box is ticked");

        _modeService.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Config);
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        sut.ShowPasswordBanner.Should().BeFalse();
    }

    [Fact]
    public void SavePasswords_Ticked_RecordsSameRowsWithPasswordsKept()
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
    public void FieldText_EditAfterSavePasswordsTicked_StillRecordsTheBox()
    {
        var sut = CreateSut();
        sut.SavePasswords = true;
        sut.AddRow();

        FieldOf(sut.Rows[0], "name").Text = "Marco";

        Recorded().SavePasswords.Should().BeTrue();
    }

    [Fact]
    public void SavePasswords_PasswordEndingInSpace_ReachesConfigFileAndComesBackWhole()
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
    public async Task CommitComboBoxSelection_ParentFlippedAwayAndBack_KeepsRows()
    {
        var parent = CreateParent();
        var list = CreateSut();
        SetupLoad(parent, list);
        var feature = CreateFeature();
        await feature.LoadSettingsAsync();

        parent.CommitComboBoxSelection(CreateTheAccountsNow);
        list.EffectiveIsEnabled.Should().BeTrue();
        list.AddRow();
        FieldOf(list.Rows[0], "name").Text = "Marco";
        FieldOf(list.Rows[0], "auto-logon").Checked = true;
        list.AddRow();
        FieldOf(list.Rows[1], "name").Text = "Guest";

        parent.CommitComboBoxSelection(LocalAccountOnly);
        list.EffectiveIsEnabled.Should().BeFalse(because: "a greyed card is how the closed gate shows");

        parent.CommitComboBoxSelection(CreateTheAccountsNow);

        list.Rows.Select(row => FieldOf(row, "name").Text).Should().Equal("Marco", "Guest");
        FieldOf(list.Rows[0], "auto-logon").Checked.Should().BeTrue();
        Recorded().Rows.Select(row => row.Values.GetValueOrDefault("name")).Should().Equal("Marco", "Guest");
        SignsIn(Recorded()).Should().Be("Marco",
            because: "a card that shows the right rows and files the wrong ones is the same bug to the user");
    }

    // Opening the page again, a language change or a filter change throws every card away and builds a new one.
    [Fact]
    public async Task LoadSettingsAsync_RebuildBetweenParentFlips_BringsRowsBack()
    {
        var parent = CreateParent();
        var list = CreateSut();
        SetupLoad(parent, list);
        var feature = CreateFeature();
        await feature.LoadSettingsAsync();

        parent.CommitComboBoxSelection(CreateTheAccountsNow);
        list.AddRow();
        FieldOf(list.Rows[0], "name").Text = "Marco";
        FieldOf(list.Rows[0], "password").Text = "hunter2";
        FieldOf(list.Rows[0], "auto-logon").Checked = true;
        list.SavePasswords = true;
        parent.CommitComboBoxSelection(LocalAccountOnly);

        // The factory applies the overlay as the last thing it does, so a rebuilt card arrives carrying it.
        var rebuiltParent = CreateParent();
        var rebuiltList = CreateSut();
        rebuiltParent.ApplyAuthoredOverlay();
        rebuiltList.ApplyAuthoredOverlay();
        SetupLoad(rebuiltParent, rebuiltList);
        await feature.RefreshSettingsAsync();

        rebuiltParent.CommitComboBoxSelection(CreateTheAccountsNow);

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
    public void FieldText_RowTypedOutOfOrder_IsRecordedWholeOnceNamed()
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
    public void OptionIndex_SignInTickedUnderWrongGroup_DoesNotComeBackWithGroupChange()
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
    public void OptionIndex_GroupMovedAway_DropsSignInAlreadyTaken()
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
