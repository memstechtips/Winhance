using System.Globalization;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class TimeRegionLanguageServiceTests
{
    private const string Zones = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones";
    private const string Info = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation";
    private const string International = @"HKEY_CURRENT_USER\Control Panel\International";
    private const string Geo = @"HKEY_CURRENT_USER\Control Panel\International\Geo";
    private const string Layouts = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Keyboard Layouts";
    private const string Preload = @"HKEY_CURRENT_USER\Keyboard Layout\Preload";
    private const string Substitutes = @"HKEY_CURRENT_USER\Keyboard Layout\Substitutes";
    private const string Language = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Nls\Language";

    // CP_ACP 0, CP_OEMCP 1, CP_MACCP 2, CP_THREAD_ACP 3 - the Win32 "use the system's" constants.
    private const int HighestCodePageSentinel = 3;

    private const string GroupingShape = "^[0-9](;[0-9])*$";

    private static readonly TimeRegionLanguageService Service = new();

    private static readonly OptionProviderRegistry Providers = new(() => new IOptionProvider[] { Service });

    private static readonly string[] FormatValueNames =
    [
        "LocaleName", "Locale", "sDecimal", "sThousand", "sList", "sShortDate", "sLongDate", "sTimeFormat",
        "sShortTime", "iFirstDayOfWeek", "iMeasure", "sMonDecimalSep", "sMonThousandSep", "sCurrency", "sNegativeSign",
        "sGrouping", "sMonGrouping", "iCurrDigits", "iCurrency", "iNegCurr", "iNegNumber", "s1159", "s2359", "sDate",
        "sTime", "sYearMonth", "iDate", "iTime", "iTLZero", "iTimePrefix", "iFirstWeekOfYear",
    ];

    private static readonly string[] PatternValueNames =
    [
        "sShortDate", "sLongDate", "sTimeFormat", "sShortTime", "sYearMonth", "s1159", "s2359",
    ];

    private static readonly string[] SweptCultures = ["en-US", "de-DE", "es-ES", "fr-FR", "ja-JP", "af-ZA"];

    private static readonly string[] GeoValueNames = ["Nation", "Name"];

    private static readonly string[] OfferedKlids = ["00000409", "00000809", "00010409"];

    private static readonly string[] OfferedLayoutTexts = ["US", "United Kingdom", "United States-Dvorak"];

    private static readonly string[] SentinelMacCultures = ["eu-ES", "ro-MD", "ru-MD", "ur-IN"];

    private static readonly string[] CodePageTargetKeys = ["ACP", "OEMCP", "MACCP"];

    // en-US has both an LCID and a region; without those the format leaves Locale and iMeasure out.
    private static readonly (string Id, string Key)[] KeyPerSetting =
    [
        ("region-time-zone", "UTC"),
        ("region-format", "en-US"),
        ("region-home-location", "US"),
        ("region-keyboard-layout", "00000409"),
        ("region-system-locale", "en-US"),
    ];

    private static Setting TimeZone => SettingCatalog.Find("region-time-zone")!;

    private static Setting Format => SettingCatalog.Find("region-format")!;

    private static Setting HomeLocation => SettingCatalog.Find("region-home-location")!;

    private static Setting KeyboardLayout => SettingCatalog.Find("region-keyboard-layout")!;

    private static Setting SystemLocale => SettingCatalog.Find("region-system-locale")!;

    private static IReadOnlyDictionary<string, StateValue>? SetFor(Setting setting, string key) =>
        KeyedOptions.SetFor(setting, key, Service);

    private static IEnumerable<string> WrittenTargetKeys(Setting setting) =>
        setting.Targets.OfType<RegTarget>().Where(t => !t.ReadOnly).Select(t => t.Key);

    private static FakeDetectionContext ZoneMachine() =>
        new FakeDetectionContext()
            .Sub(Zones, "UTC", "South Africa Standard Time", "Pacific Standard Time", "Broken Zone")
            .Set($@"{Zones}\UTC", "Display", "(UTC) Coordinated Universal Time")
            .Set($@"{Zones}\UTC", "TZI", Tzi(0))
            .Set($@"{Zones}\South Africa Standard Time", "Display", "(UTC+02:00) Harare, Pretoria")
            .Set($@"{Zones}\South Africa Standard Time", "TZI", Tzi(-120))
            .Set($@"{Zones}\Pacific Standard Time", "Display", "(UTC-08:00) Pacific Time (US & Canada)")
            .Set($@"{Zones}\Pacific Standard Time", "TZI", Tzi(480));

    // REG_TZI_FORMAT is 44 bytes opening with the LONG Bias; nothing after it decides the order.
    private static byte[] Tzi(int bias)
    {
        var value = new byte[44];
        BitConverter.GetBytes(bias).CopyTo(value, 0);
        return value;
    }

    // 0000dead is a well-formed KLID with no Layout Text.
    private static FakeDetectionContext LayoutMachine() =>
        new FakeDetectionContext()
            .Sub(Layouts, "00000409", "00000809", "00010409", "0000dead")
            .Set($@"{Layouts}\00000409", "Layout Text", "US")
            .Set($@"{Layouts}\00000809", "Layout Text", "United Kingdom")
            .Set($@"{Layouts}\00010409", "Layout Text", "United States-Dvorak");

    private static string GeoIdOf(string isoCode) =>
        new RegionInfo(isoCode).GeoId.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Spaced(string text) => text.Replace('\u202f', ' ').Replace('\u00a0', ' ');

    private static IReadOnlyList<ApplyOp>? PlanFor(string settingId, string key) =>
        ApplyRequestResolver.Resolve(settingId, enable: true, value: key, resetToDefault: false, options: Providers);

    [Fact]
    public void Every_zone_with_a_display_name_is_offered_in_ascending_utc_offset_order()
    {
        var options = Service.Options(TimeZone, ZoneMachine());

        options.Select(o => o.Value).Should().Equal(
            "Pacific Standard Time", "UTC", "South Africa Standard Time");
        options[0].Label.Should().Be("(UTC-08:00) Pacific Time (US & Canada)");
    }

    [Fact]
    public void Two_zones_at_one_offset_are_ordered_by_their_display_name()
    {
        var machine = ZoneMachine()
            .Sub(Zones, "South Africa Standard Time", "Egypt Standard Time")
            .Set($@"{Zones}\Egypt Standard Time", "Display", "(UTC+02:00) Cairo")
            .Set($@"{Zones}\Egypt Standard Time", "TZI", Tzi(-120));

        Service.Options(TimeZone, machine).Select(o => o.Value).Should().Equal(
            "Egypt Standard Time", "South Africa Standard Time");
    }

    [Fact]
    public void A_zone_with_no_readable_TZI_is_offered_last()
    {
        // Its Display claims +00:00, so this fails the moment the label is trusted over the TZI.
        var machine = ZoneMachine()
            .Sub(Zones, "UTC", "South Africa Standard Time", "Pacific Standard Time", "Nowhere Standard Time")
            .Set($@"{Zones}\Nowhere Standard Time", "Display", "(UTC+00:00) Nowhere");

        Service.Options(TimeZone, machine).Select(o => o.Value).Should().Equal(
            "Pacific Standard Time", "UTC", "South Africa Standard Time", "Nowhere Standard Time");
    }

    [Fact]
    public void A_zone_with_no_display_name_is_not_offered()
    {
        Service.Options(TimeZone, ZoneMachine()).Should().NotContain(o => o.Value == "Broken Zone");
    }

    [Fact]
    public void The_live_zone_is_the_TimeZoneKeyName_value()
    {
        var machine = ZoneMachine().Set(Info, "TimeZoneKeyName", "South Africa Standard Time");

        Service.CurrentKey(TimeZone, machine).Should().Be("South Africa Standard Time");
    }

    [Fact]
    public void A_machine_with_no_TimeZoneKeyName_has_no_selection()
    {
        Service.CurrentKey(TimeZone, ZoneMachine()).Should().BeNull();
    }

    [Fact]
    public void Applying_a_zone_writes_its_key_name_and_nothing_else()
    {
        var set = SetFor(TimeZone, "UTC");

        set.Should().NotBeNull();
        set!.Keys.Should().Equal("TimeZoneKeyName");
        set.Keys.Should().Equal(WrittenTargetKeys(TimeZone));
        set["TimeZoneKeyName"].WritePayload.Should().Be("UTC");
    }

    [Fact]
    public void A_second_zone_writes_its_own_key_name()
    {
        var set = SetFor(TimeZone, "Pacific Standard Time");

        set!["TimeZoneKeyName"].WritePayload.Should().Be("Pacific Standard Time");
    }

    [Fact]
    public void An_empty_key_writes_nothing()
    {
        SetFor(TimeZone, "").Should().BeNull();
    }

    [Fact]
    public void A_time_zone_setting_is_a_keyed_selection()
    {
        TimeZone.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void The_fake_answers_subkeys_and_existence_under_either_spelling_of_the_hive()
    {
        var machine = ZoneMachine();

        machine.GetSubKeyNames(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones").Should().Equal(
            "UTC", "South Africa Standard Time", "Pacific Standard Time", "Broken Zone");
        machine.KeyExists(Zones).Should().BeTrue();
        machine.KeyExists($@"{Zones}\UTC").Should().BeTrue();
        machine.GetSubKeyNames(Info).Should().BeEmpty();
        machine.KeyExists(Info).Should().BeFalse();
    }

    [Fact]
    public void Applying_a_zone_also_asks_tzutil_to_switch_the_running_clock()
    {
        var effect = Service.EffectFor(TimeZone, "South Africa Standard Time");

        effect.Should().BeOfType<ScriptEffect>();
        var script = (ScriptEffect)effect!;
        script.Script.Should().Be("tzutil.exe /s 'South Africa Standard Time'");
        script.Run.Should().Be(RunContext.System);
    }

    [Fact]
    public void A_second_zone_gets_its_own_tzutil_argument()
    {
        var script = (ScriptEffect)Service.EffectFor(TimeZone, "UTC")!;

        script.Script.Should().Be("tzutil.exe /s 'UTC'");
    }

    [Fact]
    public void A_list_that_needs_no_nudge_declares_none()
    {
        Service.EffectFor(Format, "en-US").Should().BeNull();
        Service.EffectFor(SystemLocale, "en-US").Should().BeNull();
        Service.EffectFor(HomeLocation, "US").Should().BeNull();
    }

    [Fact]
    public void Applying_the_time_zone_setting_emits_the_registry_write_and_the_nudge()
    {
        var plan = PlanFor("region-time-zone", "UTC");

        var write = Assert.IsType<RegistryWriteOp>(plan![0]);
        write.Value.Should().Be("UTC");
        var effect = Assert.IsType<EffectOp>(plan[1]).Effect;
        var script = Assert.IsType<ScriptEffect>(effect);
        script.Script.Should().Be("tzutil.exe /s 'UTC'");
        script.Run.Should().Be(RunContext.System);
    }

    [Theory]
    [InlineData("UTC\"; Start-Process calc; \"")]
    [InlineData("UTC$(Start-Process calc)")]
    [InlineData("UTC`nStart-Process calc")]
    [InlineData("UTC\u2019; Start-Process calc; \u2018")]
    [InlineData("UTC'; Start-Process calc; '")]
    [InlineData("UTC\r\nStart-Process calc")]
    [InlineData("UTC;calc")]
    public void A_key_that_could_break_out_of_the_tzutil_command_is_not_a_zone(string key)
    {
        SetFor(TimeZone, key).Should().BeNull();
        Service.EffectFor(TimeZone, key).Should().BeNull();
    }

    [Theory]
    [InlineData("UTC+12")]
    [InlineData("UTC-02")]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Dateline Standard Time")]
    [InlineData("Central Standard Time (Mexico)")]
    [InlineData("Mid-Atlantic Standard Time")]
    [InlineData("Russia Time Zone 10")]
    public void A_windows_zone_id_with_spaces_and_punctuation_is_accepted_and_quoted_whole(string key)
    {
        SetFor(TimeZone, key)!["TimeZoneKeyName"].WritePayload.Should().Be(key);
        var script = Assert.IsType<ScriptEffect>(Service.EffectFor(TimeZone, key));
        script.Script.Should().Be($"tzutil.exe /s '{key}'");
    }

    [Fact]
    public void Every_zone_this_pc_has_is_a_key_the_setting_accepts()
    {
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
            Service.Accepts(TimeZone, zone.Id).Should().BeTrue(zone.Id);
    }

    [Fact]
    public void A_zone_id_longer_than_windows_stores_is_not_a_zone()
    {
        SetFor(TimeZone, new string('A', 127)).Should().NotBeNull();
        SetFor(TimeZone, new string('A', 128)).Should().BeNull();
    }

    [Fact]
    public void A_hostile_zone_key_from_a_config_resolves_to_no_plan()
    {
        PlanFor("region-time-zone", "UTC\"; Start-Process calc; \"").Should().BeNull();
    }

    [Fact]
    public void Every_setting_writes_exactly_the_value_names_it_declares_as_written_targets()
    {
        // ApplyPlanBuilder walks Targets, not the Set: a name only the service knows is written nowhere, a target only
        // the setting declares is written with nothing, and either way the apply reports Succeeded.
        foreach (var (id, key) in KeyPerSetting)
        {
            var setting = SettingCatalog.Find(id)!;
            var set = SetFor(setting, key);

            set.Should().NotBeNull($"{id} must be able to apply {key}");
            set!.Should().NotBeEmpty(id);
            set!.Keys.Should().BeEquivalentTo(WrittenTargetKeys(setting), id);
        }
    }

    [Fact]
    public void The_machines_specific_cultures_are_offered_by_culture_name()
    {
        var options = Service.Options(Format, new FakeDetectionContext());

        options.Should().Contain(o => o.Value == "en-US");
        options.Should().Contain(o => o.Value == "de-DE");
        options.Should().NotContain(o => o.Value == "en", "a neutral culture is not a regional format");
        options.Single(o => o.Value == "en-US").Label.Should().Be(CultureInfo.GetCultureInfo("en-US").DisplayName);
    }

    [Fact]
    public void The_offered_cultures_come_out_sorted_by_their_display_name()
    {
        var labels = Service.Options(Format, new FakeDetectionContext()).Select(o => o.Label).ToList();

        labels.Should().BeInAscendingOrder(StringComparer.CurrentCulture);
    }

    [Fact]
    public void The_live_format_is_the_LocaleName_value()
    {
        var machine = new FakeDetectionContext().Set(International, "LocaleName", "de-DE");

        Service.CurrentKey(Format, machine).Should().Be("de-DE");
    }

    [Fact]
    public void A_machine_with_no_LocaleName_has_no_selection()
    {
        Service.CurrentKey(Format, new FakeDetectionContext()).Should().BeNull();
    }

    [Fact]
    public void Applying_en_US_writes_the_whole_American_format()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var set = SetFor(Format, "en-US");

        set.Should().NotBeNull();
        set!["LocaleName"].WritePayload.Should().Be("en-US");
        set["Locale"].WritePayload.Should().Be("00000409");
        set["sDecimal"].WritePayload.Should().Be(".");
        set["sThousand"].WritePayload.Should().Be(",");
        set["sList"].WritePayload.Should().Be(",");
        set["sShortDate"].WritePayload.Should().Be("M/d/yyyy");
        set["sLongDate"].WritePayload.Should().Be("dddd, MMMM d, yyyy");
        set["sTimeFormat"].WritePayload.Should().Be("h:mm:ss tt");
        set["sShortTime"].WritePayload.Should().Be("h:mm tt");
        set["iFirstDayOfWeek"].WritePayload.Should().Be("6");
        set["iMeasure"].WritePayload.Should().Be("1");
        set["sMonDecimalSep"].WritePayload.Should().Be(".");
        set["sMonThousandSep"].WritePayload.Should().Be(",");
        set["sCurrency"].WritePayload.Should().Be("$");
        set["sNegativeSign"].WritePayload.Should().Be("-");
        set["sGrouping"].WritePayload.Should().Be("3;0");
        set["sMonGrouping"].WritePayload.Should().Be("3;0");
        set["iCurrDigits"].WritePayload.Should().Be("2");
        set["iCurrency"].WritePayload.Should().Be("0");
        set["iNegCurr"].WritePayload.Should().Be(Invariant(culture.NumberFormat.CurrencyNegativePattern));
        set["iNegNumber"].WritePayload.Should().Be("1");
        set["s1159"].WritePayload.Should().Be("AM");
        set["s2359"].WritePayload.Should().Be("PM");
        set["sDate"].WritePayload.Should().Be("/");
        set["sTime"].WritePayload.Should().Be(":");
        set["sYearMonth"].WritePayload.Should().Be(Spaced(culture.DateTimeFormat.YearMonthPattern));
        set["iDate"].WritePayload.Should().Be("0");
        set["iTime"].WritePayload.Should().Be("0");
        set["iTLZero"].WritePayload.Should().Be("0");
        set["iTimePrefix"].WritePayload.Should().Be("0");
        set["iFirstWeekOfYear"].WritePayload.Should().Be("0");
    }

    [Fact]
    public void Applying_de_DE_writes_the_German_format()
    {
        var set = SetFor(Format, "de-DE");

        set!["LocaleName"].WritePayload.Should().Be("de-DE");
        set["Locale"].WritePayload.Should().Be("00000407");
        set["sDecimal"].WritePayload.Should().Be(",");
        set["sThousand"].WritePayload.Should().Be(".");
        set["sList"].WritePayload.Should().Be(";");
        set["sShortDate"].WritePayload.Should().Be("dd.MM.yyyy");
        set["sTimeFormat"].WritePayload.Should().Be("HH:mm:ss");
        set["iFirstDayOfWeek"].WritePayload.Should().Be("0");
        set["iMeasure"].WritePayload.Should().Be("0");
    }

    [Fact]
    public void Applying_de_DE_rewrites_what_an_American_format_left_behind()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        var number = german.NumberFormat;
        var date = german.DateTimeFormat;
        string clock = TimeRegionLanguageService.Unquoted(date.LongTimePattern);

        var set = SetFor(Format, "de-DE")!;
        var american = SetFor(Format, "en-US")!;

        set["sGrouping"].WritePayload.Should().Be(TimeRegionLanguageService.WindowsGrouping(number.NumberGroupSizes));
        set["sMonGrouping"].WritePayload.Should().Be(TimeRegionLanguageService.WindowsGrouping(number.CurrencyGroupSizes));
        set["iCurrDigits"].WritePayload.Should().Be(Invariant(number.CurrencyDecimalDigits));
        set["iCurrency"].WritePayload.Should().Be(Invariant(number.CurrencyPositivePattern));
        set["iNegCurr"].WritePayload.Should().Be(Invariant(number.CurrencyNegativePattern));
        set["iNegNumber"].WritePayload.Should().Be(Invariant(number.NumberNegativePattern));
        set["s1159"].WritePayload.Should().Be(Spaced(date.AMDesignator));
        set["s2359"].WritePayload.Should().Be(Spaced(date.PMDesignator));
        set["sDate"].WritePayload.Should().Be(".");
        set["sTime"].WritePayload.Should().Be(date.TimeSeparator);
        set["sYearMonth"].WritePayload.Should().Be(Spaced(date.YearMonthPattern));
        set["iDate"].WritePayload.Should().Be("1");
        set["iTime"].WritePayload.Should().Be("1");
        set["iTLZero"].WritePayload.Should().Be(TimeRegionLanguageService.HourLeadingZeroOf(clock));
        set["iTimePrefix"].WritePayload.Should().Be(TimeRegionLanguageService.TimeMarkerPositionOf(clock));
        set["iFirstWeekOfYear"].WritePayload.Should().Be(Invariant((int)date.CalendarWeekRule));

        set["iCurrency"].WritePayload.Should().NotBe(american["iCurrency"].WritePayload);
        set["iDate"].WritePayload.Should().NotBe(american["iDate"].WritePayload);
        set["iTime"].WritePayload.Should().NotBe(american["iTime"].WritePayload);
    }

    [Theory]
    [InlineData(new[] { 3 }, "3;0")]
    [InlineData(new[] { 3, 2 }, "3;2;0")]
    [InlineData(new[] { 3, 0 }, "3")]
    [InlineData(new[] { 0 }, "0")]
    [InlineData(new int[0], "0")]
    public void Group_sizes_are_written_the_way_LOCALE_SGROUPING_spells_them(int[] sizes, string expected)
    {
        TimeRegionLanguageService.WindowsGrouping(sizes).Should().Be(expected);
    }

    [Theory]
    [InlineData("HH:mm:ss", "HH:mm:ss")]
    [InlineData("dddd, d 'de' MMMM 'de' yyyy", "dddd, d  MMMM  yyyy")]
    [InlineData("H 'h' mm", "H  mm")]
    public void Quoted_text_is_left_out_of_a_picture_before_its_parts_are_read(string picture, string expected)
    {
        TimeRegionLanguageService.Unquoted(picture).Should().Be(expected);
    }

    [Theory]
    [InlineData("M/d/yyyy", "0")]
    [InlineData("dd.MM.yyyy", "1")]
    [InlineData("yyyy-MM-dd", "2")]
    [InlineData("'den' yyyy-MM-dd", "2")]
    [InlineData("'-'", null)]
    public void The_date_order_is_the_first_date_part_outside_quoted_text(string picture, string? expected)
    {
        TimeRegionLanguageService.DateOrderOf(TimeRegionLanguageService.Unquoted(picture)).Should().Be(expected);
    }

    [Theory]
    [InlineData("HH:mm:ss", "1")]
    [InlineData("hh:mm:ss tt", "1")]
    [InlineData("H:mm:ss", "0")]
    [InlineData("h:mm:ss tt", "0")]
    public void A_two_letter_hour_is_an_hour_with_a_leading_zero(string clock, string expected)
    {
        TimeRegionLanguageService.HourLeadingZeroOf(clock).Should().Be(expected);
    }

    [Theory]
    [InlineData("tt h:mm:ss", "1")]
    [InlineData("h:mm:ss tt", "0")]
    [InlineData("HH:mm:ss", "0")]
    [InlineData("tt", "0")]
    public void The_time_marker_leads_only_when_it_stands_before_the_hour(string clock, string expected)
    {
        TimeRegionLanguageService.TimeMarkerPositionOf(clock).Should().Be(expected);
    }

    [Fact]
    public void Every_offered_format_writes_numbers_windows_can_read()
    {
        foreach (var option in Service.Options(Format, new FakeDetectionContext()))
        {
            var culture = CultureInfo.GetCultureInfo(option.Value);
            string shortDate = TimeRegionLanguageService.Unquoted(culture.DateTimeFormat.ShortDatePattern);

            Service.ValueFor(OptionValue.GroupSizes, option.Value).Should().MatchRegex(GroupingShape, option.Value);
            Service.ValueFor(OptionValue.CurrencyGroupSizes, option.Value).Should().MatchRegex(GroupingShape, option.Value);

            if (Service.ValueFor(OptionValue.CurrencyNegativePattern, option.Value) is { } negativeCurrency)
                negativeCurrency.Should().MatchRegex("^([0-9]|1[0-5])$", option.Value);
            else
                culture.NumberFormat.CurrencyNegativePattern.Should().BeGreaterThan(15, option.Value);

            if (Service.ValueFor(OptionValue.DateOrder, option.Value) is { } dateOrder)
                dateOrder.Should().MatchRegex("^[0-2]$", option.Value);
            else
                shortDate.Should().NotContainAny("M", "d", "y");
        }
    }

    [Fact]
    public void A_culture_this_machine_does_not_have_writes_nothing()
    {
        SetFor(Format, "zz-ZZ").Should().BeNull();
    }

    [Fact]
    public void No_written_pattern_carries_a_narrow_no_break_space()
    {
        // Win32 reads U+202F as a literal it cannot render, so a pattern carrying one shows up in the clock as typed.
        foreach (var culture in SweptCultures)
        {
            var set = SetFor(Format, culture);

            foreach (var name in PatternValueNames)
            {
                var pattern = set![name].WritePayload!.ToString()!;
                pattern.Should().NotContain("\u202f", $"{culture}/{name}");
                pattern.Should().NotContain("\u00a0", $"{culture}/{name}");
            }
        }
    }

    [Fact]
    public void A_quoted_literal_in_a_pattern_is_written_as_it_stands()
    {
        // Win32 date pictures copy single-quoted text unchanged; unquoted, the d of the Spanish de is a day token.
        SetFor(Format, "es-ES")!["sLongDate"].WritePayload.Should().Be("dddd, d 'de' MMMM 'de' yyyy");
    }

    [Fact]
    public void A_regional_format_setting_is_a_keyed_selection()
    {
        Format.Control.Should().Be(ControlKind.KeyedSelection);
        SetFor(Format, "en-US")!.Keys.Should().BeEquivalentTo(FormatValueNames);
        SetFor(Format, "en-US")!.Keys.Should().BeEquivalentTo(WrittenTargetKeys(Format));
    }

    [Fact]
    public void A_culture_with_no_LCID_omits_Locale_and_en_US_keeps_its_hex_value()
    {
        var noLcidCulture = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .First(c => c.LCID == TimeRegionLanguageService.NoLcid);

        SetFor(Format, noLcidCulture.Name)!.Keys.Should()
            .BeEquivalentTo(FormatValueNames.Where(name => name != "Locale"));
        SetFor(Format, "en-US")!["Locale"].Should().BeEquivalentTo(StateValue.Of("00000409"));
    }

    [Fact]
    public void Every_region_is_offered_once_keyed_by_its_iso_code()
    {
        var options = Service.Options(HomeLocation, new FakeDetectionContext());

        options.Should().Contain(o => o.Value == "US");
        options.Should().Contain(o => o.Value == "ZA");
        options.Select(o => o.Value).Should().OnlyHaveUniqueItems(
            "there is one United States however many cultures name it");
        options.Single(o => o.Value == "US").Label.Should().Be(new RegionInfo("US").DisplayName);
    }

    [Fact]
    public void An_area_that_is_not_a_country_is_not_offered()
    {
        // es-419, en-001 and en-150 build a RegionInfo whose "ISO code" is a UN M49 area number, which has no GeoId.
        var values = Service.Options(HomeLocation, new FakeDetectionContext()).Select(o => o.Value).ToList();

        values.Should().NotContain("001");
        values.Should().NotContain("150");
        values.Should().NotContain("419");
    }

    [Fact]
    public void The_offered_regions_come_out_sorted_by_their_display_name()
    {
        var labels = Service.Options(HomeLocation, new FakeDetectionContext()).Select(o => o.Label).ToList();

        labels.Should().BeInAscendingOrder(StringComparer.CurrentCulture);
    }

    [Fact]
    public void The_live_region_is_the_Nation_geo_id_resolved_back_to_its_iso_code()
    {
        var machine = new FakeDetectionContext().Set(Geo, "Nation", GeoIdOf("ZA"));

        Service.CurrentKey(HomeLocation, machine).Should().Be("ZA");
    }

    [Fact]
    public void An_American_machine_reads_back_as_the_United_States()
    {
        // US is the GeoId a region with no geography data falls back to, so it breaks first if the lookup walks cultures.
        var machine = new FakeDetectionContext().Set(Geo, "Nation", GeoIdOf("US"));

        Service.CurrentKey(HomeLocation, machine).Should().Be("US");
    }

    [Fact]
    public void A_Nation_no_region_claims_falls_back_to_the_Name_value()
    {
        var machine = new FakeDetectionContext()
            .Set(Geo, "Nation", "999999")
            .Set(Geo, "Name", "GB");

        Service.CurrentKey(HomeLocation, machine).Should().Be("GB");
    }

    [Fact]
    public void A_machine_with_neither_value_has_no_selection()
    {
        Service.CurrentKey(HomeLocation, new FakeDetectionContext()).Should().BeNull();
    }

    [Fact]
    public void Applying_a_region_writes_the_geo_id_and_the_iso_code()
    {
        var set = SetFor(HomeLocation, "US");

        set.Should().NotBeNull();
        set!.Keys.Should().BeEquivalentTo(GeoValueNames);
        set["Name"].WritePayload.Should().Be("US");
        set["Nation"].WritePayload.Should().Be(GeoIdOf("US"));
        set["Nation"].WritePayload!.ToString().Should().MatchRegex("^[0-9]+$", "Nation is a decimal GeoId, not a code");
    }

    [Fact]
    public void A_second_region_writes_a_different_geo_id()
    {
        var us = SetFor(HomeLocation, "US")!["Nation"].WritePayload;
        var za = SetFor(HomeLocation, "ZA")!["Nation"].WritePayload;

        za.Should().NotBe(us);
    }

    [Fact]
    public void A_region_whose_cultures_carry_no_geography_still_writes_its_own_geo_id()
    {
        // A RegionInfo built from a culture with no geography data reports the US GeoId; some of Canada's have none.
        var set = SetFor(HomeLocation, "CA");

        set!["Nation"].WritePayload.Should().Be(GeoIdOf("CA"));
        set["Nation"].WritePayload.Should().NotBe(GeoIdOf("US"));
    }

    [Fact]
    public void A_region_that_is_not_a_region_writes_nothing()
    {
        SetFor(HomeLocation, "ZZ").Should().BeNull();
    }

    [Fact]
    public void A_home_location_setting_is_a_keyed_selection()
    {
        HomeLocation.Control.Should().Be(ControlKind.KeyedSelection);
        SetFor(HomeLocation, "US")!.Keys.Should().BeEquivalentTo(WrittenTargetKeys(HomeLocation));
    }

    [Fact]
    public void Every_layout_with_a_layout_text_is_offered_sorted_by_that_text()
    {
        var options = Service.Options(KeyboardLayout, LayoutMachine());

        options.Select(o => o.Value).Should().BeEquivalentTo(OfferedKlids);
        // Windows NLS and ICU order these labels differently, so the expectation sorts with the service's comparer.
        options.Select(o => o.Label).Should().Equal(
            OfferedLayoutTexts.OrderBy(text => text, StringComparer.CurrentCulture));
    }

    [Fact]
    public void Two_layouts_sharing_a_layout_text_are_ordered_by_their_klid()
    {
        var machine = LayoutMachine()
            .Sub(Layouts, "00020409", "00000409")
            .Set($@"{Layouts}\00020409", "Layout Text", "US");

        Service.Options(KeyboardLayout, machine).Select(o => o.Value).Should().Equal("00000409", "00020409");
    }

    [Fact]
    public void A_layout_with_no_layout_text_is_not_offered()
    {
        Service.Options(KeyboardLayout, LayoutMachine()).Should().NotContain(o => o.Value == "0000dead");
    }

    [Fact]
    public void The_live_layout_is_the_first_preload_entry()
    {
        var machine = LayoutMachine().Set(Preload, "1", "00000809");

        Service.CurrentKey(KeyboardLayout, machine).Should().Be("00000809");
    }

    [Fact]
    public void A_substituted_preload_reports_the_layout_that_actually_loads()
    {
        var machine = LayoutMachine()
            .Set(Preload, "1", "00000409")
            .Set(Substitutes, "00000409", "00010409");

        Service.CurrentKey(KeyboardLayout, machine).Should().Be("00010409");
    }

    // A real machine writes a lowercase KLID into Preload and the same layout in uppercase under Substitutes.
    [Fact]
    public void A_lowercase_preload_finds_its_uppercase_substitute()
    {
        var machine = LayoutMachine()
            .Set(Preload, "1", "00001c09")
            .Set(Substitutes, "00001C09", "00000409");

        Service.CurrentKey(KeyboardLayout, machine).Should().Be("00000409");
    }

    [Fact]
    public void A_machine_with_no_preload_has_no_selection()
    {
        Service.CurrentKey(KeyboardLayout, LayoutMachine()).Should().BeNull();
    }

    [Fact]
    public void Applying_a_layout_writes_the_first_preload_entry()
    {
        var set = SetFor(KeyboardLayout, "00000809");

        set.Should().NotBeNull();
        set!.Keys.Should().Equal("Preload1");
        set.Keys.Should().Equal(WrittenTargetKeys(KeyboardLayout));
        set["Preload1"].WritePayload.Should().Be("00000809");
    }

    [Fact]
    public void A_second_layout_writes_its_own_klid()
    {
        var set = SetFor(KeyboardLayout, "00010409");

        set!["Preload1"].WritePayload.Should().Be("00010409");
    }

    [Fact]
    public void A_lowercase_klid_is_a_layout_id()
    {
        var set = SetFor(KeyboardLayout, "00001c09");

        set!["Preload1"].WritePayload.Should().Be("00001c09");
    }

    [Fact]
    public void A_key_that_is_not_a_layout_id_writes_nothing()
    {
        SetFor(KeyboardLayout, "US").Should().BeNull();
        SetFor(KeyboardLayout, "0409").Should().BeNull();
        SetFor(KeyboardLayout, "zzzzzzzz").Should().BeNull();
        SetFor(KeyboardLayout, "").Should().BeNull();
    }

    [Fact]
    public void Applying_a_layout_also_removes_the_substitute_filed_under_its_id()
    {
        var removal = Assert.IsType<RegContentEffect>(Service.EffectFor(KeyboardLayout, "00000409"));

        removal.Content.Should().Be(
            $"Windows Registry Editor Version 5.00\r\n\r\n[{Substitutes}]\r\n\"00000409\"=-\r\n");
        ApplyOpScriptEmitter.MixesHives(removal.Content).Should().BeFalse();
    }

    [Fact]
    public void Applying_a_layout_emits_the_preload_write_and_then_the_substitute_removal()
    {
        var plan = PlanFor("region-keyboard-layout", "00000809");

        plan.Should().HaveCount(2);
        var write = Assert.IsType<RegistryWriteOp>(plan![0]);
        write.Path.Should().Be(Preload);
        write.Value.Should().Be("00000809");
        var removal = Assert.IsType<RegContentEffect>(Assert.IsType<EffectOp>(plan[1]).Effect);
        removal.Content.Should().Contain("\"00000809\"=-");
        ApplyPlan.From(plan).AsyncEffects.Should().Equal(removal);
    }

    [Fact]
    public void A_key_that_is_not_a_layout_id_gets_no_substitute_removal()
    {
        Service.EffectFor(KeyboardLayout, "0409\"=-\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE]").Should().BeNull();
        Service.EffectFor(KeyboardLayout, "zzzzzzzz").Should().BeNull();
    }

    [Fact]
    public void The_answer_file_script_runs_tzutil_for_the_system_and_the_substitute_removal_for_the_user()
    {
        var emitter = new ApplyOpScriptEmitter(Mock.Of<ILogService>(), Providers);

        var choices = new[]
        {
            new SettingChoice("region-time-zone", new ChoiceValue.Keyed("W. Europe Standard Time", "(UTC+01:00) Berlin")),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000409", "US")),
        };

        var result = emitter.Emit(
            new SelectionSet(choices, [], []),
            new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.TimeRegionLanguage] = [TimeZone, KeyboardLayout] },
            new WinBuild(26100),
            systemIndent: "    ",
            userIndent: "        ");

        result.Warnings.Should().BeEmpty();
        string system = result.SystemPassByFeature[FeatureIds.TimeRegionLanguage];
        string user = result.UserPassByFeature[FeatureIds.TimeRegionLanguage];
        system.Should().Contain("tzutil.exe /s 'W. Europe Standard Time'").And.NotContain("Substitutes");
        user.Should().Contain("\"00000409\"=-").And.Contain("reg import").And.NotContain("tzutil");
    }

    [Fact]
    public void A_keyboard_layout_setting_is_a_keyed_selection()
    {
        KeyboardLayout.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void Every_culture_windows_can_set_as_the_system_locale_is_offered()
    {
        var options = Service.Options(SystemLocale, new FakeDetectionContext());

        options.Should().Contain(o => o.Value == "en-US");
        options.Should().Contain(o => o.Value == "ja-JP");
        options.Should().Contain(o => o.Value == "de-DE");
        options.Select(o => o.Value).Should().Contain(
            SentinelMacCultures, "a sentinel Mac code page is not a reason to drop a locale");
        options.Single(o => o.Value == "en-US").Label.Should().Be(CultureInfo.GetCultureInfo("en-US").DisplayName);
        options.Should().OnlyContain(o => CultureInfo.GetCultureInfo(o.Value).LCID != TimeRegionLanguageService.NoLcid);
    }

    [Fact]
    public void A_culture_with_no_code_pages_of_its_own_is_not_a_system_locale()
    {
        // Hindi is Unicode-only: no ANSI code page at all, and the CP_OEMCP / CP_MACCP sentinels for the other
        // two. Windows leaves such a locale out of the language-for-non-Unicode-programs list.
        var options = Service.Options(SystemLocale, new FakeDetectionContext());

        options.Should().NotContain(o => o.Value == "hi-IN");
        SetFor(SystemLocale, "hi-IN").Should().BeNull();
    }

    [Fact]
    public void The_offered_system_locales_come_out_sorted_by_their_display_name()
    {
        var labels = Service.Options(SystemLocale, new FakeDetectionContext()).Select(o => o.Label).ToList();

        labels.Should().BeInAscendingOrder(StringComparer.CurrentCulture);
    }

    [Fact]
    public void The_live_locale_is_the_hex_lcid_under_Nls_Language()
    {
        var machine = new FakeDetectionContext().Set(Language, "Default", "0409");

        Service.CurrentKey(SystemLocale, machine).Should().Be("en-US");
    }

    [Fact]
    public void The_live_locale_is_reported_even_when_it_is_not_offered()
    {
        var machine = new FakeDetectionContext().Set(Language, "Default", "0439");

        Service.CurrentKey(SystemLocale, machine).Should().Be("hi-IN");
    }

    [Fact]
    public void A_hex_lcid_no_culture_claims_has_no_selection()
    {
        var machine = new FakeDetectionContext().Set(Language, "Default", "FFFF");

        Service.CurrentKey(SystemLocale, machine).Should().BeNull();
    }

    [Fact]
    public void A_machine_with_no_Default_value_has_no_selection()
    {
        Service.CurrentKey(SystemLocale, new FakeDetectionContext()).Should().BeNull();
    }

    [Theory]
    [InlineData("en-US", "0409", "1252", "437", "10000")]
    [InlineData("ja-JP", "0411", "932", "932", "10001")]
    [InlineData("de-DE", "0407", "1252", "850", "10000")]
    // Basque reports the CP_MACCP sentinel, so MACCP falls back to Mac Roman rather than naming a sentinel.
    [InlineData("eu-ES", "042D", "1252", "850", "10000")]
    public void Applying_a_culture_writes_its_lcid_and_all_three_code_pages(
        string key, string lcid, string ansi, string oem, string mac)
    {
        var set = SetFor(SystemLocale, key);

        set.Should().NotBeNull();
        set!.Keys.Should().BeEquivalentTo(WrittenTargetKeys(SystemLocale));
        set["Default"].WritePayload.Should().Be(lcid);
        set["ACP"].WritePayload.Should().Be(ansi);
        set["OEMCP"].WritePayload.Should().Be(oem);
        set["MACCP"].WritePayload.Should().Be(mac);
    }

    [Fact]
    public void A_system_locale_this_machine_does_not_have_writes_nothing()
    {
        SetFor(SystemLocale, "zz-ZZ").Should().BeNull();
    }

    [Fact]
    public void Every_offered_system_locale_writes_a_well_formed_set()
    {
        var declared = WrittenTargetKeys(SystemLocale).ToList();

        foreach (var option in Service.Options(SystemLocale, new FakeDetectionContext()))
        {
            var set = SetFor(SystemLocale, option.Value);

            set.Should().NotBeNull(option.Value);
            set!.Keys.Should().BeEquivalentTo(declared, option.Value);

            foreach (var name in CodePageTargetKeys)
            {
                // A sentinel would name the code page the machine already has, not the one the chosen locale needs.
                int.Parse((string)set[name].WritePayload!, CultureInfo.InvariantCulture)
                    .Should().BeGreaterThan(HighestCodePageSentinel, $"{option.Value}/{name}");
            }
        }
    }

    [Fact]
    public void A_system_locale_setting_is_a_keyed_selection()
    {
        SystemLocale.Control.Should().Be(ControlKind.KeyedSelection);
    }
}
