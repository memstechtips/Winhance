using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Autounattend.ViewModels;
using Winhance.UI.Features.Common.Interfaces;
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
        FeatureIds.ExplorerCustomization,
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
        _externalApps.Object);

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
    public void The_page_header_falls_back_to_English_when_the_keys_are_missing()
    {
        var sut = CreateSut();

        sut.PageTitle.Should().Be("Autounattend.xml Settings");
        sut.PageDescription.Should().Be("Automate the Windows Setup Process");
    }

    [Fact]
    public void The_page_header_takes_the_translations_when_they_are_there()
    {
        _localization.PresentKey("Autounattend_Page_Title", "Unbeaufsichtigt");
        _localization.PresentKey("Autounattend_Page_Description", "Windows-Setup automatisieren");

        var sut = CreateSut();

        sut.PageTitle.Should().Be("Unbeaufsichtigt");
        sut.PageDescription.Should().Be("Windows-Setup automatisieren");
    }

    [Fact]
    public void A_language_change_re_raises_the_page_header()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        RaiseLanguageChanged();

        raised.Should().Contain(nameof(sut.PageTitle));
        raised.Should().Contain(nameof(sut.PageDescription));
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
    public async Task The_app_rows_count_what_is_ticked_out_of_what_the_catalog_offers()
    {
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[0].Children[0].CountText.Should().Be("2 of 5 included");
        sut.IncludedGroups[0].Children[1].CountText.Should().Be("1 of 4 included");
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
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedTitle.Should().Be("Aus anderen Bereichen");
        sut.IncludedGroups[0].Children[0].CountText.Should().Be("5 moechten, 2 gewaehlt");
    }

    [Fact]
    public async Task A_broken_translation_of_the_count_format_still_shows_both_numbers()
    {
        _localization.PresentKey("Autounattend_Included_Count", "{0 of {1} included");
        var sut = CreateSut();

        await sut.LoadIncludedAsync();

        sut.IncludedGroups[0].Children[0].CountText.Should().Be("2/5");
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
        sut.IncludedGroups[0].Children[0].CountText.Should().Be("2 of 5 included");
    }
}
