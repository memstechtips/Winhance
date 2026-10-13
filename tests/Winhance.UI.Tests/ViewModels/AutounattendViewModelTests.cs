using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Autounattend.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.WimUtil.Models;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AutounattendViewModelTests
{
    private readonly Mock<ISettingsLoadingService> _settingsLoading = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<IApplicationModeService> _mode = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ICatalogScopeProvider> _scope = new();
    private readonly Mock<IAppSelectionSource> _apps = new();
    private readonly Mock<IWindowsAppsService> _windowsApps = new();
    private readonly Mock<IExternalAppsService> _externalApps = new();
    private readonly Mock<ISubscriptionToken> _subscription = new();
    private readonly Mock<IFilePickerService> _filePicker = new();
    private readonly Mock<IApplicationModeService> _builder = new();
    private readonly Dictionary<string, SettingChoice> _authored = new();
    private readonly WimUtilSession _session = new();
    private Func<FilterStateChangedEvent, Task>? _filterStateChangedHandler;

    private static readonly int GroupedSettings = SettingCatalog.ByFeature
        .Where(kv => !FeatureDefinitions.AutounattendFeatures.Contains(kv.Key))
        .Sum(kv => kv.Value.Count);

    private static readonly bool[] OptimizeOpenOnly = [false, true, false];

    private static readonly string[] SoftwareAppsOrder = [FeatureIds.WindowsApps, FeatureIds.ExternalApps];

    private static readonly string[] OptimizeOrder =
    [
        FeatureIds.Privacy,
        FeatureIds.Power,
        FeatureIds.GamingPerformance,
        FeatureIds.Update,
        FeatureIds.Notifications,
        FeatureIds.Sound,
    ];

    private static readonly string[] CustomizeOrder =
    [
        FeatureIds.WindowsTheme,
        FeatureIds.Taskbar,
        FeatureIds.StartMenu,
        FeatureIds.Explorer,
        FeatureIds.TimeRegionLanguage,
    ];

    public AutounattendViewModelTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => $"[{key}]");
        _localization.MirrorTryGetString();

        _dispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback<Action>(action => action());
        _dispatcher.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(action => action());

        _scope.SetupGet(s => s.Current).Returns(CatalogScope.CurrentMachine);
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _mode.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
        _registry.Setup(r => r.GetByFeature(It.IsAny<string>(), It.IsAny<CatalogScope>()))
            .Returns((string featureId, CatalogScope _) => SettingCatalog.ByFeature[featureId]);

        _apps.Setup(a => a.CheckedWindowsAppsAsync())
            .ReturnsAsync((IReadOnlyList<AppChoice>)[App("a"), App("b")]);
        _apps.Setup(a => a.CheckedExternalAppsAsync())
            .ReturnsAsync((IReadOnlyList<AppChoice>)[App("c")]);
        _windowsApps.Setup(s => s.GetAppsAsync())
            .ReturnsAsync((IEnumerable<ItemDefinition>)Definitions(5));
        _externalApps.Setup(s => s.GetAppsAsync())
            .ReturnsAsync((IEnumerable<ItemDefinition>)Definitions(4));

        _eventBus
            .Setup(e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()))
            .Callback<Func<FilterStateChangedEvent, Task>>(handler => _filterStateChangedHandler = handler)
            .Returns(_subscription.Object);
    }

    private Task RaiseFilterStateChanged() =>
        _filterStateChangedHandler!(new FilterStateChangedEvent(false));

    private void RaiseLanguageChanged() =>
        _localization.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

    private static AppChoice App(string id) => new(id, id, null, null, null, null);

    private static List<ItemDefinition> Definitions(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new ItemDefinition { Id = $"app-{i}", Name = $"App {i}", Description = "x" })
            .ToList();

    private static Dictionary<string, string> EnglishStrings() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")))!;

    private AutounattendViewModel CreateSut() => new(
        _settingsLoading.Object,
        _log.Object,
        _localization.Object,
        _dispatcher.Object,
        _eventBus.Object,
        _mode.Object,
        _registry.Object,
        _scope.Object,
        _apps.Object,
        _windowsApps.Object,
        _externalApps.Object,
        _session);

    private static ExternalAppsRowViewModel ExternalAppsRow(AutounattendViewModel sut) =>
        sut.IncludedGroups[0].Children[1].Should().BeOfType<ExternalAppsRowViewModel>().Subject;

    // The page's own card, driven the way the page reads it: a Selection's SelectedValue is its state index.
    private SettingItemViewModel WinhanceOnNewPcCard(int state)
    {
        var setting = SettingCatalog.Find("autounattend-desktop-shortcut")!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = "Winhance on the new PC",
                Description = string.Empty,
                InputType = InputType.Selection,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object)
        {
            SelectedValue = state,
        };
    }

    [Fact]
    public void ModuleId_is_the_Autounattend_feature()
    {
        CreateSut().ModuleId.Should().Be(FeatureIds.Autounattend);
    }

    [Fact]
    public void DisplayName_comes_from_the_feature_name_key()
    {
        _localization.PresentKey("Feature_Autounattend_Name", "Unattend");

        CreateSut().DisplayName.Should().Be("Unattend");
    }

    [Fact]
    public async Task Search_text_inside_the_page_name_still_filters_the_cards()
    {
        _localization.PresentKey("Feature_Autounattend_Name", "Autounattend");
        var sut = CreateSut();
        var card = WinhanceOnNewPcCard(0);
        sut.Settings.Add(card);
        var filtered = new TaskCompletionSource();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.IsVisibleInSearch))
                filtered.TrySetResult();
        };

        sut.SearchText = "auto";
        await filtered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        card.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void The_cards_start_empty_and_not_loading()
    {
        var sut = CreateSut();

        sut.Settings.Should().BeEmpty();
        sut.GroupedSettings.Should().BeEmpty();
        sut.IsLoading.Should().BeFalse();
        sut.SettingsCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_is_safe_to_call_twice()
    {
        var sut = CreateSut();

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Before_the_load_the_summary_card_shows_a_placeholder_and_no_rows()
    {
        var sut = CreateSut();

        sut.IncludedCountText.Should().Be("...");
        sut.IncludedGroups.Should().BeEmpty();
        sut.IncludedCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4));
    }

    [Fact]
    public async Task The_load_builds_three_group_rows_with_their_features_under_them()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups.Should().HaveCount(3);
        sut.IncludedGroups.Select(g => g.Name).Should().Equal("Software & Apps", "Optimize", "Customize");
        sut.IncludedGroups.Select(g => g.Key).Should().Equal("Nav_SoftwareAndApps", "Nav_Optimize", "Nav_Customize");
        sut.IncludedGroups[0].Children.Select(c => c.Key).Should().Equal(SoftwareAppsOrder);
        sut.IncludedGroups[1].Children.Select(c => c.Key).Should().Equal(OptimizeOrder);
        sut.IncludedGroups[2].Children.Select(c => c.Key).Should().Equal(CustomizeOrder);
        sut.IncludedGroups[0].Children.Select(c => c.Name).Should().Equal("Windows Apps & Features", "External Software");
    }

    [Fact]
    public async Task Every_row_draws_the_icon_the_sidebar_or_its_own_page_draws()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups.Select(g => g.IconResourceKey)
            .Should().Equal("SoftwareAppsIconSymbol", "OptimizeIconSymbol", "CustomizeIconSymbol");
        foreach (var feature in sut.IncludedGroups.SelectMany(g => g.Children))
        {
            feature.IconResourceKey.Should().Be(FeatureDefinitions.Get(feature.Key)!.IconKey);
        }
    }

    [Fact]
    public async Task Every_row_label_key_exists_in_en_json_and_the_fallback_matches_its_English()
    {
        var en = EnglishStrings();
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        var rows = sut.IncludedGroups
            .Select(group => (Key: group.Key, group.Name))
            .Concat(sut.IncludedGroups.SelectMany(g => g.Children)
                .Select(feature => (Key: AutounattendViewModel.IncludedLabelKey(feature.Key), feature.Name)))
            .ToList();

        rows.Should().HaveCount(16);
        foreach (var (key, name) in rows)
        {
            en.Should().ContainKey(key);
            en[key].Should().Be(name, key);
        }
    }

    [Fact]
    public void A_feature_with_no_mapped_label_is_refused_loudly()
    {
        var act = () => AutounattendViewModel.IncludedLabelKey("WindowsCapabilities");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task The_app_rows_say_what_is_removed_and_what_Winhance_installs()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[0].Children[0].CountText.Should().Be("2 will be removed from Windows");
        sut.IncludedGroups[0].Children[1].CountText.Should().Be("1 will be installed by Winhance");
        sut.IncludedGroups[0].CountText.Should().Be("3 of 9 included");
    }

    [Fact]
    public async Task A_settings_row_counts_what_is_included_out_of_what_is_in_scope()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        var privacy = SettingCatalog.ByFeature[FeatureIds.Privacy].Count;
        sut.IncludedGroups[1].Children[0].CountText.Should().Be($"{privacy} of {privacy} included");
    }

    [Fact]
    public async Task A_setting_the_filters_hide_is_not_counted_on_either_side()
    {
        _registry.Setup(r => r.GetByFeature(FeatureIds.Sound, It.IsAny<CatalogScope>()))
            .Returns(SettingCatalog.ByFeature[FeatureIds.Sound].Take(2).ToList());
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[1].Children[5].CountText.Should().Be("2 of 2 included");
    }

    [Fact]
    public async Task The_settings_count_drops_what_the_user_left_out()
    {
        var privacy = SettingCatalog.ByFeature[FeatureIds.Privacy];
        var excluded = privacy.Take(2).Select(setting => setting.Id).ToHashSet(StringComparer.Ordinal);
        _mode.Setup(m => m.IsIncluded(It.IsAny<string>()))
            .Returns<string>(id => !excluded.Contains(id));

        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.IncludedGroups[1].Children[0].CountText.Should().Be($"{privacy.Count - 2} of {privacy.Count} included");
    }

    [Fact]
    public async Task The_card_totals_the_three_groups()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        var settings = GroupedSettings;
        sut.IncludedCountText.Should().Be($"{settings + 3} of {settings + 9} included");
    }

    [Fact]
    public async Task Only_the_last_row_still_on_screen_rounds_its_bottom_corners()
    {
        var flat = new Microsoft.UI.Xaml.CornerRadius(0);
        var rounded = new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.IncludedGroups[0].CornerRadius.Should().Be(flat);
        sut.IncludedGroups[2].CornerRadius.Should().Be(rounded);

        sut.IncludedGroups[2].ToggleExpander();

        sut.IncludedGroups[2].CornerRadius.Should().Be(flat);
        sut.IncludedGroups[2].Children[^1].CornerRadius.Should().Be(rounded);
    }

    [Fact]
    public async Task Collapsing_the_summary_card_rounds_all_four_of_its_corners()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.IncludedCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4, 4, 0, 0));

        sut.ToggleIncludedExpander();

        sut.IncludedCornerRadius.Should().Be(new Microsoft.UI.Xaml.CornerRadius(4));
    }

    [Fact]
    public async Task A_source_that_throws_leaves_the_placeholder_and_logs_rather_than_taking_the_page_down()
    {
        _apps.Setup(a => a.CheckedWindowsAppsAsync()).ThrowsAsync(new InvalidOperationException("no provider"));
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedCountText.Should().Be("...");
        sut.IncludedGroups.Should().BeEmpty();
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("no provider")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task A_present_key_wins_over_the_English_fallback()
    {
        _localization.PresentKey("Autounattend_Included_Name", "Aus anderen Bereichen");
        _localization.PresentKey("Autounattend_Included_Count", "{1} moechten, {0} gewaehlt");
        _localization.PresentKey("Autounattend_Included_WindowsApps_Count", "{0} werden entfernt");
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedTitle.Should().Be("Aus anderen Bereichen");
        sut.IncludedGroups[0].CountText.Should().Be("9 moechten, 3 gewaehlt");
        sut.IncludedGroups[0].Children[0].CountText.Should().Be("2 werden entfernt");
    }

    [Fact]
    public async Task A_broken_translation_of_the_count_format_still_shows_both_numbers()
    {
        _localization.PresentKey("Autounattend_Included_Count", "{0 of {1} included");
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[0].CountText.Should().Be("3/9");
    }

    [Fact]
    public async Task The_filter_subscription_outlives_a_second_host_until_both_have_released()
    {
        // The Unattend page and WIMUtil's card host one view model, and the new host can load before the old unloads.
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        await sut.LoadIncludedAsync();

        sut.ReleaseIncluded();
        _subscription.Verify(s => s.Dispose(), Times.Never);

        sut.ReleaseIncluded();
        _subscription.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task An_expanded_group_row_stays_expanded_after_a_second_load()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        sut.IncludedGroups[1].ToggleExpander();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[1].Key.Should().Be("Nav_Optimize");
        sut.IncludedGroups.Select(g => g.IsExpanded).Should().Equal(OptimizeOpenOnly);
    }

    [Fact]
    public async Task A_throwing_relabel_is_caught_and_logged()
    {
        _localization.Setup(l => l.GetString("Nav_Optimize")).Throws(new InvalidOperationException("bad label"));
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedCountText.Should().Be("...");
        sut.IncludedGroups.Should().BeEmpty();
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("bad label")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task A_superseded_load_that_throws_does_not_clear_the_card()
    {
        var firstLoad = new TaskCompletionSource<IReadOnlyList<AppChoice>>();
        var calls = 0;
        _apps.Setup(a => a.CheckedWindowsAppsAsync())
            .Returns(() => ++calls == 1
                ? firstLoad.Task
                : Task.FromResult((IReadOnlyList<AppChoice>)[App("a"), App("b")]));
        var sut = CreateSut();
        var settings = GroupedSettings;

        var superseded = sut.LoadIncludedAsync();
        await sut.LoadIncludedAsync();
        sut.IncludedCountText.Should().Be($"{settings + 3} of {settings + 9} included");

        firstLoad.SetException(new InvalidOperationException("too late"));
        await superseded;

        sut.IncludedCountText.Should().Be($"{settings + 3} of {settings + 9} included");
        sut.IncludedGroups.Should().HaveCount(3);
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("too late")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Flipping_the_hardware_filter_counts_the_settings_rows_again()
    {
        var soundInScope = true;
        _registry.Setup(r => r.GetByFeature(FeatureIds.Sound, It.IsAny<CatalogScope>()))
            .Returns(() => soundInScope ? SettingCatalog.ByFeature[FeatureIds.Sound] : Array.Empty<Setting>());
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var sound = SettingCatalog.ByFeature[FeatureIds.Sound].Count;
        sut.IncludedGroups[1].Children[5].CountText.Should().Be($"{sound} of {sound} included");

        soundInScope = false;
        await RaiseFilterStateChanged();

        sut.IncludedGroups[1].Children[5].CountText.Should().Be("0 of 0 included");
    }

    [Fact]
    public async Task The_filter_subscription_is_taken_once_however_often_the_page_loads()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();
        await sut.LoadIncludedAsync();

        _eventBus.Verify(
            e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task Release_drops_the_filter_subscription_and_the_next_load_takes_it_again()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();

        sut.ReleaseIncluded();
        _subscription.Verify(token => token.Dispose(), Times.Once);

        await sut.LoadIncludedAsync();

        _eventBus.Verify(
            e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()),
            Times.Exactly(2));
    }

    [Fact]
    public void The_summary_card_names_a_material_glyph_for_IconConverter()
    {
        // IconConverter builds a PathIcon, which needs the XAML runtime, so only the glyph name can be pinned here.
        CreateSut().IncludedIcon.Should().Be("FormatListBulleted");
    }

    [Fact]
    public async Task A_language_change_relabels_the_rows_and_keeps_the_counts()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        _localization.PresentKey("Nav_Optimize", "Optimieren");

        RaiseLanguageChanged();

        sut.IncludedGroups[1].Name.Should().Be("Optimieren");
        sut.IncludedGroups[0].Children[0].CountText.Should().Be("2 will be removed from Windows");
    }

    [Fact]
    public async Task Only_the_external_apps_row_carries_the_install_options()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[0].Children[0].Should().BeOfType<IncludedRowViewModel>();
        sut.IncludedGroups[0].Children[1].Should().BeOfType<ExternalAppsRowViewModel>();
    }

    private const string Reader = "C:\\Setup\\Reader.exe";
    private const string Vpn = "C:\\Setup\\Vpn.msi";

    // The two cards the row draws, built the way the page's own cards are and writing into a Builder session.
    private SettingItemViewModel InstallerCard(string id, InputType inputType, string name)
    {
        _builder.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _builder.SetupGet(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
        _builder.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(edit => _authored[edit.SettingId] = edit);
        var setting = SettingCatalog.Find(id)!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = name,
                Description = name + ", described",
                InputType = inputType,
                IsSelected = inputType == InputType.CheckBox,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object, _builder.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object,
            applicationModeService: _builder.Object,
            filePickerService: _filePicker.Object)
        {
            ListAddLabel = "Add an installer",
            ListRemoveLabel = "Remove this installer",
        };
    }

    private (SettingItemViewModel Stage, SettingItemViewModel Installers) LoadInstallerCards(
        AutounattendViewModel sut, params SettingItemViewModel[] others)
    {
        var stage = InstallerCard(OwnInstaller.StageAppsId, InputType.CheckBox, "Put the installers on the ISO");
        var installers = InstallerCard(OwnInstaller.SettingId, InputType.List, "Your own installers");
        sut.Settings = [.. others, stage, installers];
        sut.GroupedSettings.Add(new SettingsGroup("General", [.. others, stage, installers]));
        return (stage, installers);
    }

    private IReadOnlyList<OwnInstaller> RecordedInstallers() =>
        OwnInstaller.From(new SelectionSet([_authored[OwnInstaller.SettingId]], [], []));

    private void ArrangePicks(params string[] paths)
    {
        var picks = _filePicker.SetupSequence(p => p.PickFile(It.IsAny<string[]>(), It.IsAny<string?>()));
        foreach (var path in paths)
            picks = picks.Returns(path);
    }

    [Fact]
    public async Task The_install_options_show_once_their_cards_are_loaded_with_or_without_a_WIMUtil_folder()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        ExternalAppsRow(sut).HasInstallerCards.Should().BeFalse();

        LoadInstallerCards(sut);

        _session.WorkingDirectory.Should().BeNull();
        ExternalAppsRow(sut).HasInstallerCards.Should().BeTrue();
    }

    [Fact]
    public async Task A_reloaded_page_hands_the_row_its_new_cards()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var row = ExternalAppsRow(sut);
        var (staleStage, _) = LoadInstallerCards(sut);

        sut.GroupedSettings.Clear();
        row.HasInstallerCards.Should().BeFalse();
        var (stage, _) = LoadInstallerCards(sut);
        staleStage.IsSelected = false;

        row.HasInstallerCards.Should().BeTrue();
        row.StageOnMedia.Should().BeTrue(because: "the row reads the card the page shows now");
        stage.IsSelected.Should().BeTrue();
    }

    // With the media's editions loaded every other media card shows on the grouped list; these two never do.
    [Fact]
    public void The_installer_cards_are_drawn_by_the_row_and_stay_off_the_grouped_list()
    {
        _session.Editions = [new(1, "Windows 11 Home", "Core")];
        var sut = CreateSut();

        var (stage, installers) = LoadInstallerCards(sut);

        stage.ParentIsVisible.Should().BeFalse();
        installers.ParentIsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task Unticking_the_media_checkbox_records_the_choice_and_a_rebuilt_row_still_reads_it()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = ExternalAppsRow(sut);
        row.StageOnMedia.Should().BeTrue();

        row.StageOnMedia = false;

        _authored[OwnInstaller.StageAppsId].Value.Should().Be(new ChoiceValue.CheckBox(false));
        row.StageOnMedia.Should().BeFalse();
        await sut.LoadIncludedAsync();
        ExternalAppsRow(sut).Should().NotBeSameAs(row);
        ExternalAppsRow(sut).StageOnMedia.Should().BeFalse();
    }

    [Fact]
    public async Task Adding_an_installer_adds_a_row_and_records_it()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = ExternalAppsRow(sut);

        row.AddOwnInstaller();

        RecordedInstallers().Should().Equal(new OwnInstaller(Reader, string.Empty));
        row.OwnInstallerRows.Should().ContainSingle()
            .Which.FieldFor("file")!.Text.Should().Be(Reader);
        row.HasOwnInstallers.Should().BeTrue();
        _filePicker.Verify(p => p.PickFile(It.Is<string[]>(f => f[1] == "*.exe;*.msi"), "Add an installer"), Times.Once);
    }

    [Fact]
    public async Task A_cancelled_pick_adds_nothing()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = ExternalAppsRow(sut);

        row.AddOwnInstaller();

        row.OwnInstallerRows.Should().BeEmpty();
        _authored.Should().NotContainKey(OwnInstaller.SettingId);
    }

    [Fact]
    public async Task Typing_switches_records_them_on_that_installer()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = ExternalAppsRow(sut);
        row.AddOwnInstaller();

        row.OwnInstallerRows!.Single().FieldFor("switches")!.Text = " /sAll ";

        RecordedInstallers().Should().Equal(new OwnInstaller(Reader, "/sAll"));
    }

    [Fact]
    public async Task Remove_takes_the_row_and_its_record_away_down_to_none()
    {
        ArrangePicks(Reader, Vpn);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        LoadInstallerCards(sut);
        var row = ExternalAppsRow(sut);
        row.AddOwnInstaller();
        row.AddOwnInstaller();

        row.OwnInstallerRows![0].Remove();

        RecordedInstallers().Should().Equal(new OwnInstaller(Vpn, string.Empty));
        row.OwnInstallerRows.Should().ContainSingle()
            .Which.FieldFor("file")!.Text.Should().Be(Vpn);

        row.OwnInstallerRows[0].Remove();

        RecordedInstallers().Should().BeEmpty();
        row.HasOwnInstallers.Should().BeFalse();
    }

    [Fact]
    public async Task The_row_shows_the_cards_own_text_and_follows_it()
    {
        ArrangePicks(Reader);
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var (stage, installers) = LoadInstallerCards(sut);
        var row = ExternalAppsRow(sut);
        row.AddOwnInstaller();

        row.StageOnMediaLabel.Should().Be("Put the installers on the ISO");
        row.OwnInstallersName.Should().Be("Your own installers");
        row.OwnInstallersDescription.Should().Be("Your own installers, described");
        row.AddOwnInstallerLabel.Should().Be("Add an installer");
        row.OwnInstallerRows!.Single().RemoveLabel.Should().Be("Remove this installer");

        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        stage.Name = "Installer auf die ISO";
        installers.Name = "Eigene Installer";

        row.StageOnMediaLabel.Should().Be("Installer auf die ISO");
        row.OwnInstallersName.Should().Be("Eigene Installer");
        raised.Should().Contain(nameof(ExternalAppsRowViewModel.StageOnMediaLabel))
            .And.Contain(nameof(ExternalAppsRowViewModel.OwnInstallersName));
    }

    [Fact]
    public async Task The_row_reads_out_how_Winhance_reaches_the_new_PC_and_follows_the_card()
    {
        var sut = CreateSut();
        await sut.LoadIncludedAsync();
        var row = ExternalAppsRow(sut);
        row.Description.Should().Be("Installed from the 'Install Winhance' desktop shortcut",
            because: "until the card loads it reads as its seed state");

        var card = WinhanceOnNewPcCard(2);
        sut.GroupedSettings.Add(new SettingsGroup("General", [card]));
        row.Description.Should().Be("Installed automatically after setup");

        card.SelectedValue = 1;

        row.Description.Should().Be("Installed from the 'Install Winhance' desktop shortcut");
        row.IsWarning.Should().BeFalse();
    }

    [Fact]
    public async Task Dont_add_with_apps_checked_warns_instead_of_reading_out()
    {
        var sut = CreateSut();
        sut.GroupedSettings.Add(new SettingsGroup("General", [WinhanceOnNewPcCard(0)]));

        await sut.LoadIncludedAsync();

        var row = ExternalAppsRow(sut);
        row.IsWarning.Should().BeTrue();
        row.WarningText.Should().Be("'Winhance on the new PC' is set to 'Don't add', so these apps will not be installed.");
        row.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task With_nothing_to_install_the_row_neither_reads_out_nor_warns_until_an_installer_is_added()
    {
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync((IReadOnlyList<AppChoice>)[]);
        ArrangePicks(Reader);
        var sut = CreateSut();
        LoadInstallerCards(sut, WinhanceOnNewPcCard(0));
        await sut.LoadIncludedAsync();
        var row = ExternalAppsRow(sut);
        row.IsWarning.Should().BeFalse();
        row.Description.Should().BeEmpty();

        row.AddOwnInstaller();

        row.IsWarning.Should().BeTrue();
    }

    [Fact]
    public async Task The_external_apps_row_falls_back_to_the_English_in_en_json()
    {
        var en = EnglishStrings();
        var sut = CreateSut();
        sut.GroupedSettings.Add(new SettingsGroup("General", [WinhanceOnNewPcCard(2)]));

        await sut.LoadIncludedAsync();

        var row = ExternalAppsRow(sut);
        sut.IncludedGroups[0].Children[0].CountText
            .Should().Be(en["Autounattend_Included_WindowsApps_Count"].Replace("{0}", "2"));
        row.CountText.Should().Be(en["Autounattend_Included_ExternalApps_Count"].Replace("{0}", "1"));
        row.Description.Should().Be(en["Autounattend_Included_ExternalApps_Automatic"]);
        row.WarningText.Should().Be(en["Autounattend_Included_ExternalApps_NotInstalled"]);

        sut.GroupedSettings[0][0].SelectedValue = 1;

        row.Description.Should().Be(en["Autounattend_Included_ExternalApps_Shortcut"]);
    }

    private SettingItemViewModel MediaEditionsListCard()
    {
        var setting = SettingCatalog.Find("autounattend-media-editions-list")!;
        var dialogs = new Mock<IDialogService>();
        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = "Editions to keep",
                Description = string.Empty,
                InputType = InputType.CheckList,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, dialogs.Object, _localization.Object, _log.Object),
            _log.Object,
            _dispatcher.Object,
            dialogs.Object,
            _localization.Object);
    }

    [Fact]
    public void The_media_cards_stay_hidden_without_a_WIMUtil_session_and_the_rest_stay_shown()
    {
        var sut = CreateSut();
        var checklist = MediaEditionsListCard();
        var other = WinhanceOnNewPcCard(0);
        sut.Settings = [checklist, other];

        sut.GroupedSettings.Add(new SettingsGroup("General", [other]));

        checklist.ParentIsVisible.Should().BeFalse();
        checklist.CheckItems.Should().BeEmpty();
        other.ParentIsVisible.Should().BeTrue();
    }

    [Fact]
    public void A_WIMUtil_session_shows_the_media_cards_with_its_editions()
    {
        _session.Editions = [new(1, "Windows 11 Home", "Core"), new(2, "Windows 11 Pro", "Professional")];
        var sut = CreateSut();
        var checklist = MediaEditionsListCard();
        sut.Settings = [checklist];

        sut.GroupedSettings.Add(new SettingsGroup("General", [checklist]));

        checklist.ParentIsVisible.Should().BeTrue();
        checklist.CheckItems.Select(item => item.Key).Should().Equal("Windows 11 Home", "Windows 11 Pro");
    }
}
