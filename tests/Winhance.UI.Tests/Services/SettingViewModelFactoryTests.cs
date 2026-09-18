using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.UI.Tests.Services;

public class SettingViewModelFactoryTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IRegeditLauncher> _mockRegeditLauncher = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<INewBadgeService> _mockNewBadgeService = new();
    private readonly Mock<ISettingViewModelEnricher> _mockEnricher = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IFilePickerService> _mockFilePickerService = new();

    private readonly SettingViewModelDependencies _deps;
    private readonly SettingViewModelFactory _sut;

    public SettingViewModelFactoryTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(a => a().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _deps = new SettingViewModelDependencies(
            SettingWriteStrategies.Selector(
                _mockSettingApplicationService.Object,
                _mockDialogService.Object,
                _mockLocalizationService.Object,
                _mockLogService.Object,
                _mockApplicationModeService.Object),
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockRegeditLauncher.Object,
            _mockApplicationModeService.Object,
            _mockFilePickerService.Object);

        _sut = new SettingViewModelFactory(
            _deps,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockUserPreferencesService.Object,
            _mockNewBadgeService.Object,
            _mockEnricher.Object);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNonNullViewModel()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_SetsSettingId()
    {
        var setting = CreateToggleSetting("MySetting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.SettingId.Should().Be("MySetting");
    }

    [Fact]
    public async Task CreateAsync_SetsNameAndDescription()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };
        _mockLocalizationService.Setup(l => l.GetString("Setting_TestSetting_Name")).Returns("Localized Name");
        _mockLocalizationService.Setup(l => l.GetString("Setting_TestSetting_Description")).Returns("Localized Description");

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.Name.Should().Be("Localized Name");
        result.Description.Should().Be("Localized Description");
    }

    [Fact]
    public async Task CreateAsync_SetsGroupName()
    {
        var setting = CreateToggleSetting("TestSetting", groupName: "SettingGroup_PrivacySettings");
        var state = new SettingStateResult { Success = true };
        _mockLocalizationService.Setup(l => l.GetString("SettingGroup_PrivacySettings")).Returns("Localized Group");

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.GroupName.Should().Be("Localized Group");
    }

    [Fact]
    public async Task CreateAsync_SetsIsSelectedFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenNotEnabled_SetsIsSelectedToFalse()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = false, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WhenRequiresAdvancedUnlock_SetsIsLocked()
    {
        var setting = CreateToggleSetting("AdvancedSetting", requiresAdvancedUnlock: true);
        var state = new SettingStateResult { Success = true };

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false))
            .ReturnsAsync(false);

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenAdvancedUnlocked_SetsIsLockedToFalse()
    {
        var setting = CreateToggleSetting("AdvancedSetting", requiresAdvancedUnlock: true);
        var state = new SettingStateResult { Success = true };

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false))
            .ReturnsAsync(true);

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsMinMaxValues()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 50, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.MinValue.Should().Be(0);
        result.MaxValue.Should().Be(100);
        result.Units.Should().Be("ms");
    }

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsNumericValue()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 42, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.NumericValue.Should().Be(42);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_PopulatesComboBoxOptions()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.ComboBoxOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_SetsSelectedValue()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.SelectedValue.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_LeavesDetectOnlyStatesOutOfTheOptionList()
    {
        // A detect-only state is one detection can resolve to but the user cannot pick. Offering it as a
        // dropdown entry would offer a choice that writes nothing.
        var setting = CreateSelectionSettingWithDetectOnlyState("DetectOnlySetting");
        var state = new SettingStateResult { CurrentValue = 0, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.ComboBoxOptions.Should().HaveCount(2);
        result.ComboBoxOptions.Select(o => o.DisplayText).Should().NotContain("Setting_DetectOnlySetting_Option_1");
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_SurvivingOptionsKeepTheirOwnStateIndex()
    {
        // THE invariant behind the skip. Option Value == state index is what the drop-down-closed handler
        // applies, what a saved .winhance config persists, and what the review diff compares - so a skipped
        // state must NOT pull the states after it down by one. Here the skipped state sits in the MIDDLE,
        // so a renumbering filter would produce 0,1 and this test would fail.
        var setting = CreateSelectionSettingWithDetectOnlyState("DetectOnlySetting");
        var state = new SettingStateResult { CurrentValue = 0, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.ComboBoxOptions.Select(o => (int)o.Value).Should().Equal(0, 2);
    }

    [Fact]
    public async Task CreateAsync_CallsApplyReviewDiff()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };

        await _sut.CreateAsync(setting, state, null, null, null);

        _mockEnricher.Verify(e => e.ApplyReviewDiff(It.IsAny<SettingItemViewModel>(), state), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonSelectionType_SetsSelectedValueFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { CurrentValue = "SomeValue", Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.SelectedValue.Should().Be("SomeValue");
    }

    [Fact]
    public async Task CreateAsync_PassesParentViewModelToConfig()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };
        var parentVm = new Mock<ISettingsFeatureViewModel>().Object;

        var result = await _sut.CreateAsync(setting, state, parentVm, null, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_SetsOnAndOffTextFromLocalization()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Common_On"))
            .Returns("Enabled");

        _mockLocalizationService
            .Setup(l => l.GetString("Common_Off"))
            .Returns("Disabled");

        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.OnText.Should().Be("Enabled");
        result.OffText.Should().Be("Disabled");
    }

    [Fact]
    public async Task CreateAsync_SeedsTheTextBoxFromTheDetectedValueFirst()
    {
        var setting = CreateTextSetting("text-setting", "FALLBACK");
        var state = new SettingStateResult { Success = true, CurrentValue = "WINHANCE" };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.TextValue.Should().Be("WINHANCE");
    }

    [Fact]
    public async Task CreateAsync_SeedsTheTextBoxFromTheCatalogDefaultWhenNothingWasRead()
    {
        var setting = CreateTextSetting("text-setting", "FALLBACK");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.TextValue.Should().Be("FALLBACK");
    }

    // The mocked service answers every key with the key itself, so these assert the KEY the card asks for.
    [Fact]
    public async Task CreateAsync_SeedsTheListButtonCaptions()
    {
        var setting = CreateListSetting("list-setting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.ListAddLabel.Should().Be("AutounattendAccounts_Add");
        result.ListRemoveLabel.Should().Be("AutounattendAccounts_Remove");
        result.SavePasswordsLabel.Should().Be("AutounattendAccounts_SavePasswords");
    }

    // A seed applied last would put the machine's row back over the rows the user typed earlier in the session.
    [Fact]
    public async Task CreateAsync_ListCard_AppliesTheAuthoredRowsAfterTheSeed()
    {
        var setting = CreateListSetting("list-setting");
        var seeded = new ChoiceValue.List(
            [new ChoiceValue.ListRow(new Dictionary<string, string> { ["name"] = "ThisPc" })]);
        var authored = new ChoiceValue.List(
            [
                new ChoiceValue.ListRow(new Dictionary<string, string> { ["name"] = "Marco" }),
                new ChoiceValue.ListRow(new Dictionary<string, string> { ["name"] = "Standard" }),
            ],
            SavePasswords: true);
        _mockApplicationModeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _mockApplicationModeService.Setup(m => m.GetBuilderEdit("list-setting"))
            .Returns(new SettingChoice("list-setting", authored));

        var result = await _sut.CreateAsync(
            setting, new SettingStateResult { Success = true, CurrentValue = seeded }, null, null, null);

        result.Rows.Select(row => row.FieldFor("name")!.Text).Should().Equal("Marco", "Standard");
        result.SavePasswords.Should().BeTrue();
    }

    // Synthetic catalog Setting fixtures. The factory reads the passed Setting; these hand-built Settings
    // carry exactly the fields CreateAsync reads (Control -> InputType, Display, Availability, Numeric, States).
    private static Setting CreateToggleSetting(
        string id, string? name = null, string? description = null,
        string? groupName = null, bool requiresAdvancedUnlock = false) =>
        new Setting
        {
            Id = id,
            Display = new()
            {
                Name = TestKeys.Of(name ?? $"Setting_{id}_Name"),
                Description = TestKeys.Of(description ?? $"Setting_{id}_Description"),
                GroupName = groupName is null ? null : TestKeys.Of(groupName),
            },
            Availability = requiresAdvancedUnlock ? new Availability { RequiresAdvancedUnlock = true } : Availability.Everywhere,
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled },
            },
        };

    private static Setting CreateNumericRangeSetting(string id, int min, int max, string units) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of("Numeric"), Description = TestKeys.Of("Numeric setting"), GroupName = TestKeys.Of("") },
            Numeric = new() { Min = min, Max = max, Units = units },
        };

    private static Setting CreateTextSetting(string id, string? textDefault) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of("Text"), Description = TestKeys.Of("Text setting"), GroupName = TestKeys.Of("") },
            TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("That is not usable.")), textDefault),
        };

    private static Setting CreateListSetting(string id) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of("List"), Description = TestKeys.Of("List setting"), GroupName = TestKeys.Of("") },
            List = new([
                new("name", FieldKind.Text, TestKeys.Of("Field_Name"),
                    Rule: new TextRule("^.+$", UpperCase: false, TestKeys.Of("That is not a usable name."))),
            ]),
        };

    [Fact]
    public async Task CreateAsync_TileSelection_BuildsATilePerOption()
    {
        var setting = TileSelectionSetting();
        var state = new SettingStateResult
        {
            Success = true,
            DynamicOptions =
            [
                new DynamicOption("eleven", @"C:\Web\eleven.jpg"),
                new DynamicOption("ten", @"C:\Web\ten.jpg"),
            ],
            DynamicSelection = @"C:\Web\ten.jpg",
        };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.ShowsOptionTiles.Should().BeTrue();
        result.Tiles.Select(tile => tile.Value).Should().Equal(@"C:\Web\eleven.jpg", @"C:\Web\ten.jpg");
        result.Tiles.Single(tile => tile.IsSelected).Value.Should().Be(@"C:\Web\ten.jpg");
    }

    // The registry can spell a shipped picture's path in another case; it is still that picture.
    [Fact]
    public async Task CreateAsync_TileSelection_LocalizesTheWindowsPictures()
    {
        var setting = TileSelectionSetting();
        var windowsPicture = setting.States[0];
        _mockLocalizationService.Setup(l => l.GetString(windowsPicture.Label.Value)).Returns("Windows 11 light");
        var state = new SettingStateResult
        {
            Success = true,
            DynamicOptions =
            [
                new DynamicOption("img0.jpg", KeyedOptions.KeyOf(setting, windowsPicture)!.ToLowerInvariant()),
                new DynamicOption("mine.jpg", @"D:\Pictures\mine.jpg"),
            ],
        };

        var result = await _sut.CreateAsync(setting, state, null, null, null);

        result.Tiles.Select(tile => tile.Label).Should().Equal("Windows 11 light", "mine.jpg");
    }

    [Fact]
    public async Task CreateAsync_KeyedSelectionWithNoOptions_ListsNoIndexOptions()
    {
        var result = await _sut.CreateAsync(
            TileSelectionSetting(), new SettingStateResult { Success = true, CurrentValue = 0 }, null, null, null);

        result.ComboBoxOptions.Should().BeEmpty();
        result.SelectedValue.Should().BeNull(because: "the placeholder index is not a key");
    }

    [Fact]
    public async Task CreateAsync_ASelectionWithNoTileFlag_DrawsNoTiles()
    {
        var setting = CreateSelectionSetting("plain-selection");

        var result = await _sut.CreateAsync(setting, new SettingStateResult { Success = true }, null, null, null);

        result.ShowsOptionTiles.Should().BeFalse();
        result.Tiles.Should().BeEmpty();
    }

    // Windows puts pictures under "Recent images" with the caption on the Browse row, colours the other way round.
    [Fact]
    public async Task CreateAsync_TileSelection_SplitsTheHeadersAndCornersAcrossTwoRows()
    {
        var pictures = await _sut.CreateAsync(
            TileSelectionSetting(), new SettingStateResult { Success = true }, null, null, null);
        var colors = await _sut.CreateAsync(
            TileSelectionSetting(OptionTiles.Colors), new SettingStateResult { Success = true }, null, null, null);
        pictures.IsLastChild = true;

        pictures.ChildHeader.Should().Be("Setting_theme-wallpaper-picture_Recent");
        pictures.BrowseRowHeader.Should().Be(pictures.Name);
        colors.ChildHeader.Should().Be(colors.Name);
        colors.BrowseRowHeader.Should().Be("Setting_theme-wallpaper-color_Custom");
        pictures.ChildCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(0));
        pictures.BrowseRowCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4));
    }

    private static Setting CreateSelectionSetting(string id) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of("Selection"), Description = TestKeys.Of("Selection setting"), GroupName = TestKeys.Of("") },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Option A") },
                new SettingState { Label = TestKeys.Of("Option B") },
            },
        };

    private static Setting TileSelectionSetting(OptionTiles tiles = OptionTiles.Pictures) =>
        SettingCatalog.Find(tiles == OptionTiles.Colors ? "theme-wallpaper-color" : "theme-wallpaper-picture")!;

    // The detect-only state sits at index 1, DELIBERATELY not last, so the skip-not-renumber assertion
    // cannot pass by accident the way a trailing skip would.
    private static Setting CreateSelectionSettingWithDetectOnlyState(string id) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of("Selection"), Description = TestKeys.Of("Selection setting"), GroupName = TestKeys.Of("") },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Option A") },
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
                new SettingState { Label = TestKeys.Of("Option C") },
            },
        };
}
