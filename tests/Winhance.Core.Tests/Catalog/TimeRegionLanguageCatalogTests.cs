using Winhance.Core.Features.Customize.Catalogs;
using Winhance.TestSupport;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Xunit;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Tests.Catalog;

public class TimeRegionLanguageCatalogTests
{
    private static readonly string[] KeyedIds =
    [
        "region-time-zone",
        "region-format",
        "region-home-location",
        "region-keyboard-layout",
        "region-system-locale",
    ];

    [Fact]
    public void The_five_keyed_settings_are_in_the_live_catalog_under_their_own_feature()
    {
        foreach (var id in KeyedIds)
        {
            SettingCatalog.Find(id).Should().NotBeNull(id);
            SettingCatalog.ByFeature[FeatureIds.TimeRegionLanguage].Should().Contain(s => s.Id == id, id);
        }
    }

    [Fact]
    public void Each_one_derives_as_a_keyed_selection()
    {
        foreach (var id in KeyedIds)
        {
            var setting = SettingCatalog.ById[id];

            setting.Control.Should().Be(ControlKind.KeyedSelection, id);
            setting.States.Should().BeEmpty(id);
            setting.Targets.Should().NotBeEmpty(id);
            setting.Options.Should().NotBeNull(id);
        }
    }

    [Fact]
    public void Every_written_target_takes_the_value_its_setting_names_from_the_chosen_key()
    {
        // The service computes each value by From alone: a target with the wrong From writes a real but wrong
        // value and the apply still reports Succeeded.
        var expected = new Dictionary<string, (string Key, OptionValue From)[]>
        {
            ["region-time-zone"] = [("TimeZoneKeyName", OptionValue.Key)],
            ["region-format"] =
            [
                ("LocaleName", OptionValue.Key),
                ("Locale", OptionValue.LocaleId),
                ("sDecimal", OptionValue.DecimalSeparator),
                ("sThousand", OptionValue.GroupSeparator),
                ("sList", OptionValue.ListSeparator),
                ("sShortDate", OptionValue.ShortDate),
                ("sLongDate", OptionValue.LongDate),
                ("sTimeFormat", OptionValue.LongTime),
                ("sShortTime", OptionValue.ShortTime),
                ("iFirstDayOfWeek", OptionValue.FirstDayOfWeek),
                ("iMeasure", OptionValue.MeasurementSystem),
                ("sMonDecimalSep", OptionValue.CurrencyDecimalSeparator),
                ("sMonThousandSep", OptionValue.CurrencyGroupSeparator),
                ("sCurrency", OptionValue.CurrencySymbol),
                ("sNegativeSign", OptionValue.NegativeSign),
                ("sGrouping", OptionValue.GroupSizes),
                ("sMonGrouping", OptionValue.CurrencyGroupSizes),
                ("iCurrDigits", OptionValue.CurrencyDecimalDigits),
                ("iCurrency", OptionValue.CurrencyPositivePattern),
                ("iNegCurr", OptionValue.CurrencyNegativePattern),
                ("iNegNumber", OptionValue.NumberNegativePattern),
                ("s1159", OptionValue.AmDesignator),
                ("s2359", OptionValue.PmDesignator),
                ("sDate", OptionValue.DateSeparator),
                ("sTime", OptionValue.TimeSeparator),
                ("sYearMonth", OptionValue.YearMonth),
                ("iDate", OptionValue.DateOrder),
                ("iTime", OptionValue.HourClock),
                ("iTLZero", OptionValue.HourLeadingZero),
                ("iTimePrefix", OptionValue.TimeMarkerPosition),
                ("iFirstWeekOfYear", OptionValue.FirstWeekOfYear),
            ],
            ["region-home-location"] = [("Nation", OptionValue.GeoId), ("Name", OptionValue.Key)],
            ["region-keyboard-layout"] = [("Preload1", OptionValue.Key)],
            ["region-system-locale"] =
            [
                ("Default", OptionValue.SystemLocaleId),
                ("ACP", OptionValue.AnsiCodePage),
                ("OEMCP", OptionValue.OemCodePage),
                ("MACCP", OptionValue.MacCodePage),
            ],
        };

        expected.Keys.Should().BeEquivalentTo(KeyedIds);
        foreach (var (id, targets) in expected)
        {
            SettingCatalog.ById[id].Targets.OfType<RegTarget>().Where(t => !t.ReadOnly)
                .Select(t => (t.Key, t.From))
                .Should().Equal(targets, id);
        }
    }

    [Fact]
    public void Every_target_is_a_registry_target_the_engine_can_write_or_an_answer_file_element()
    {
        foreach (var id in KeyedIds)
        {
            var targets = SettingCatalog.ById[id].Targets;
            targets.OfType<RegTarget>().Should().NotBeEmpty(id);
            targets.Where(t => t is not RegTarget).Should().AllBeOfType<AutounattendElement>(id);
        }
    }

    [Fact]
    public void The_page_names_the_feature_the_way_the_sidebar_does()
    {
        FeatureIds.TimeRegionLanguage.Should().Be("TimeRegionLanguage");
        TimeRegionLanguageCatalog.FeatureId.Should().Be(FeatureIds.TimeRegionLanguage);
        FeatureDefinitions.Get(FeatureIds.TimeRegionLanguage)!.Category.Should().Be("Customize");
        FeatureDefinitions.CustomizeFeatures.Should().Contain(FeatureIds.TimeRegionLanguage);
    }

    [Fact]
    public void Applying_a_format_and_a_locale_carries_a_restart_or_a_reboot_notice()
    {
        SettingCatalog.ById["region-format"].Apply.Restart.Should().Be(new RestartProcess("intl"));
        SettingCatalog.ById["region-home-location"].Apply.Restart.Should().Be(new RestartProcess("intl"));
        SettingCatalog.ById["region-system-locale"].Apply.RequiresReboot.Should().BeTrue();
        SettingCatalog.ById["region-keyboard-layout"].Apply.Should().Be(ApplyBehavior.None,
            "the layout applies at the next sign-in and there is no affordance for that");
    }

    [Fact]
    public void The_page_draws_its_four_groups_in_the_order_the_spec_lays_out()
    {
        TimeRegionLanguageCatalog.All.Select(s => s.Display.GroupName).Distinct()
            .Should().Equal(
                LocKey.SettingGroup.Time,
                LocKey.SettingGroup.Region,
                LocKey.SettingGroup.LanguageAndKeyboard,
                LocKey.SettingGroup.Formats);
    }

    [Fact]
    public void The_six_format_settings_moved_across_with_their_ids_and_their_states_intact()
    {
        var moved = new[]
        {
            "explorer-customization-short-date",
            "explorer-customization-first-day-of-week",
            "explorer-customization-number-decimal",
            "explorer-customization-list-separator",
            "explorer-customization-measurement-system",
            "explorer-customization-currency-decimal",
        };

        foreach (var id in moved)
        {
            SettingCatalog.ByFeature[FeatureIds.TimeRegionLanguage].Should().Contain(s => s.Id == id, id);
            SettingCatalog.ByFeature[FeatureIds.ExplorerCustomization].Should().NotContain(s => s.Id == id, id);
            SettingCatalog.ById[id].Display.GroupName.Should().Be(LocKey.SettingGroup.Formats, id);
            SettingCatalog.ById[id].Control.Should().Be(ControlKind.Selection, id);
        }

        SettingCatalog.ById["explorer-customization-first-day-of-week"].States.Select(s => s.Label)
            .Should().Equal(Enumerable.Range(0, 7)
                .Select(i => TestKeys.Of($"Setting_explorer-customization-first-day-of-week_Option_{i}")));
        SettingCatalog.ById["explorer-customization-list-separator"].States[0]
            .HasRole(RoleKind.WindowsDefault).Should().BeTrue("Comma is still the Windows default");
    }

    [Fact]
    public void The_formats_group_is_applied_after_the_regional_format_that_sets_it()
    {
        var ids = SettingCatalog.ByFeature[FeatureIds.TimeRegionLanguage].Select(s => s.Id).ToList();

        ids.IndexOf("explorer-customization-short-date").Should().BeGreaterThan(ids.IndexOf("region-format"));
    }
}
