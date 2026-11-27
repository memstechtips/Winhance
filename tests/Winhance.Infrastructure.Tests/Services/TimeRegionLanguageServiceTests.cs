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

[Collection(ProcessCultureCollection.Name)]
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
        "iDigits", "iLZero", "iCountry", "iPaperSize", "iCalendarType", "sLanguage", "sNativeDigits", "NumShape",
        "sPositiveSign",
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

    private static Setting ShortDate => SettingCatalog.Find("region-short-date")!;

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

    private static IReadOnlyList<ApplyOp>? PlanFor(string settingId, string key) =>
        ApplyRequestResolver.Resolve(settingId, enable: true, value: key, resetToDefault: false, options: Providers);

    [Fact]
    public void Options_WithZones_OffersEveryZoneWithADisplayNameInAscendingUtcOffsetOrder()
    {
        var options = Service.Options(TimeZone, ZoneMachine());

        options.Select(o => o.Value).Should().Equal(
            "Pacific Standard Time", "UTC", "South Africa Standard Time");
        options[0].Label.Should().Be("(UTC-08:00) Pacific Time (US & Canada)");
    }

    [Fact]
    public void Options_WithTwoZonesAtOneOffset_OrdersThemByDisplayName()
    {
        var machine = ZoneMachine()
            .Sub(Zones, "South Africa Standard Time", "Egypt Standard Time")
            .Set($@"{Zones}\Egypt Standard Time", "Display", "(UTC+02:00) Cairo")
            .Set($@"{Zones}\Egypt Standard Time", "TZI", Tzi(-120));

        Service.Options(TimeZone, machine).Select(o => o.Value).Should().Equal(
            "Egypt Standard Time", "South Africa Standard Time");
    }

    [Fact]
    public void Options_WithAZoneWithNoReadableTzi_OffersItLast()
    {
        // Its Display claims +00:00, so this fails the moment the label is trusted over the TZI.
        var machine = ZoneMachine()
            .Sub(Zones, "UTC", "South Africa Standard Time", "Pacific Standard Time", "Nowhere Standard Time")
            .Set($@"{Zones}\Nowhere Standard Time", "Display", "(UTC+00:00) Nowhere");

        Service.Options(TimeZone, machine).Select(o => o.Value).Should().Equal(
            "Pacific Standard Time", "UTC", "South Africa Standard Time", "Nowhere Standard Time");
    }

    [Fact]
    public void Options_WithAZoneWithNoDisplayName_DoesNotOfferIt()
    {
        Service.Options(TimeZone, ZoneMachine()).Should().NotContain(o => o.Value == "Broken Zone");
    }

    [Fact]
    public void CurrentKey_ForTimeZone_IsTheTimeZoneKeyNameValue()
    {
        var machine = ZoneMachine().Set(Info, "TimeZoneKeyName", "South Africa Standard Time");

        Service.CurrentKey(TimeZone, machine).Should().Be("South Africa Standard Time");
    }

    [Fact]
    public void CurrentKey_ForTimeZoneWithNoTimeZoneKeyName_HasNoSelection()
    {
        Service.CurrentKey(TimeZone, ZoneMachine()).Should().BeNull();
    }

    [Fact]
    public void SetFor_WithAZone_WritesItsKeyNameAndNothingElse()
    {
        var set = SetFor(TimeZone, "UTC");

        set.Should().NotBeNull();
        set!.Keys.Should().Equal("TimeZoneKeyName");
        set.Keys.Should().Equal(WrittenTargetKeys(TimeZone));
        set["TimeZoneKeyName"].WritePayload.Should().Be("UTC");
    }

    [Fact]
    public void SetFor_WithASecondZone_WritesItsOwnKeyName()
    {
        var set = SetFor(TimeZone, "Pacific Standard Time");

        set!["TimeZoneKeyName"].WritePayload.Should().Be("Pacific Standard Time");
    }

    [Fact]
    public void SetFor_WithAnEmptyZoneKey_WritesNothing()
    {
        SetFor(TimeZone, "").Should().BeNull();
    }

    [Fact]
    public void Control_OnTheTimeZoneSetting_IsKeyedSelection()
    {
        TimeZone.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void GetSubKeyNames_UnderEitherSpellingOfTheHive_AnswersSubkeysAndExistence()
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
    public void EffectFor_WithAZone_AsksTzutilToSwitchTheRunningClock()
    {
        var effect = Service.EffectFor(TimeZone, "South Africa Standard Time");

        effect.Should().BeOfType<ScriptEffect>();
        var script = (ScriptEffect)effect!;
        script.Script.Should().Be("tzutil.exe /s 'South Africa Standard Time'");
        script.Run.Should().Be(RunContext.System);
    }

    [Fact]
    public void EffectFor_WithASecondZone_GetsItsOwnTzutilArgument()
    {
        var script = (ScriptEffect)Service.EffectFor(TimeZone, "UTC")!;

        script.Script.Should().Be("tzutil.exe /s 'UTC'");
    }

    [Fact]
    public void EffectFor_ForListsThatNeedNoNudge_DeclaresNone()
    {
        Service.EffectFor(Format, "en-US").Should().BeNull();
        Service.EffectFor(SystemLocale, "en-US").Should().BeNull();
        Service.EffectFor(HomeLocation, "US").Should().BeNull();
    }

    [Fact]
    public void Resolve_WithTheTimeZoneSetting_EmitsTheRegistryWriteAndTheNudge()
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
    public void SetFor_WithAKeyThatCouldBreakOutOfTheTzutilCommand_IsNotAZone(string key)
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
    public void EffectFor_WithAWindowsZoneIdWithSpacesAndPunctuation_AcceptsItAndQuotesItWhole(string key)
    {
        SetFor(TimeZone, key)!["TimeZoneKeyName"].WritePayload.Should().Be(key);
        var script = Assert.IsType<ScriptEffect>(Service.EffectFor(TimeZone, key));
        script.Script.Should().Be($"tzutil.exe /s '{key}'");
    }

    [Fact]
    public void Accepts_WithEveryZoneThisPcHas_IsTrue()
    {
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
            Service.Accepts(TimeZone, zone.Id).Should().BeTrue(zone.Id);
    }

    [Fact]
    public void SetFor_WithAZoneIdLongerThanWindowsStores_IsNotAZone()
    {
        SetFor(TimeZone, new string('A', 127)).Should().NotBeNull();
        SetFor(TimeZone, new string('A', 128)).Should().BeNull();
    }

    [Fact]
    public void Resolve_WithAHostileZoneKeyFromAConfig_ReturnsNoPlan()
    {
        PlanFor("region-time-zone", "UTC\"; Start-Process calc; \"").Should().BeNull();
    }

    [Fact]
    public void SetFor_ForEverySetting_WritesExactlyTheValueNamesItDeclaresAsWrittenTargets()
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
    public void Options_ForFormat_OffersTheMachinesSpecificCulturesByCultureName()
    {
        var options = Service.Options(Format, new FakeDetectionContext());

        options.Should().Contain(o => o.Value == "en-US");
        options.Should().Contain(o => o.Value == "de-DE");
        options.Should().NotContain(o => o.Value == "en", "a neutral culture is not a regional format");
        options.Single(o => o.Value == "en-US").Label.Should().Be(CultureInfo.GetCultureInfo("en-US").DisplayName);
    }

    [Fact]
    public void Options_ForFormat_SortsTheOfferedCulturesByDisplayName()
    {
        var labels = Service.Options(Format, new FakeDetectionContext()).Select(o => o.Label).ToList();

        labels.Should().BeInAscendingOrder(StringComparer.CurrentCulture);
    }

    [Fact]
    public void CurrentKey_ForFormat_IsTheLocaleNameValue()
    {
        var machine = new FakeDetectionContext().Set(International, "LocaleName", "de-DE");

        Service.CurrentKey(Format, machine).Should().Be("de-DE");
    }

    [Fact]
    public void CurrentKey_ForFormatWithNoLocaleName_HasNoSelection()
    {
        Service.CurrentKey(Format, new FakeDetectionContext()).Should().BeNull();
    }

    [Fact]
    public void SetFor_WithEnUs_WritesTheWholeAmericanFormat()
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
        set["iNegCurr"].WritePayload!.ToString().Should().MatchRegex("^([0-9]|1[0-5])$");
        set["iNegNumber"].WritePayload.Should().Be("1");
        set["s1159"].WritePayload.Should().Be("AM");
        set["s2359"].WritePayload.Should().Be("PM");
        set["sDate"].WritePayload.Should().Be("/");
        set["sTime"].WritePayload.Should().Be(":");
        set["sYearMonth"].WritePayload!.ToString().Should().Contain("MMMM").And.Contain("yyyy");
        set["iDate"].WritePayload.Should().Be("0");
        set["iTime"].WritePayload.Should().Be("0");
        set["iTLZero"].WritePayload.Should().Be("0");
        set["iTimePrefix"].WritePayload.Should().Be("0");
        set["iFirstWeekOfYear"].WritePayload.Should().Be("0");
    }

    [Fact]
    public void SetFor_WithDeDe_WritesTheGermanFormat()
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
    public void SetFor_WithDeDe_RewritesWhatAnAmericanFormatLeftBehind()
    {
        var set = SetFor(Format, "de-DE")!;
        var american = SetFor(Format, "en-US")!;

        set["sGrouping"].WritePayload!.ToString().Should().MatchRegex(GroupingShape);
        set["sMonGrouping"].WritePayload!.ToString().Should().MatchRegex(GroupingShape);
        set["sDate"].WritePayload.Should().Be(".");
        set["sList"].WritePayload.Should().Be(";");
        set["iDate"].WritePayload.Should().Be("1");
        set["iTime"].WritePayload.Should().Be("1");
        set["iFirstDayOfWeek"].WritePayload.Should().Be("0");

        set["iCurrency"].WritePayload.Should().NotBe(american["iCurrency"].WritePayload);
        set["iDate"].WritePayload.Should().NotBe(american["iDate"].WritePayload);
        set["iTime"].WritePayload.Should().NotBe(american["iTime"].WritePayload);
    }

    // Measured 2026-09-21 on Windows 11 24H2: what Windows itself writes when
    // that format is picked in Settings. .NET reads ICU, which differs where it matters - ICU's ar-SA short date
    // carries U+200F direction marks and its decimal separator is U+066B, neither of which Windows writes, and the
    // format cards then cannot recognise the value Winhance left behind.
    [Theory]
    [InlineData("en-US", "sShortDate", "M/d/yyyy")]
    [InlineData("en-US", "iFirstDayOfWeek", "6")]
    [InlineData("en-ZA", "sShortDate", "yyyy/MM/dd")]
    [InlineData("en-ZA", "sDecimal", ",")]
    [InlineData("de-DE", "sShortDate", "dd.MM.yyyy")]
    [InlineData("hi-IN", "sShortDate", "dd-MM-yyyy")]
    [InlineData("hi-IN", "sDecimal", ".")]
    [InlineData("ja-JP", "sShortDate", "yyyy/MM/dd")]
    [InlineData("ar-SA", "sShortDate", "dd/MM/yyyy")]
    [InlineData("ar-SA", "sDecimal", ".")]
    [InlineData("ar-SA", "sList", ";")]
    public void A_format_writes_what_windows_holds_for_that_locale(string key, string name, string expected)
    {
        SetFor(Format, key)![name].WritePayload.Should().Be(expected);
    }

    [Fact]
    public void SetFor_WithArabic_WritesNoMarkThatIcuAddsAndWindowsDoesNot()
    {
        var arabic = SetFor(Format, "ar-SA")!;

        arabic["sShortDate"].WritePayload!.ToString().Should().NotContain("\u200f");
        arabic["sDecimal"].WritePayload.Should().NotBe("\u066b");
    }

    [Fact]
    public void ValueFor_WithEveryOfferedFormat_WritesNumbersWindowsCanRead()
    {
        foreach (var option in Service.Options(Format, new FakeDetectionContext()))
        {
            Service.ValueFor(OptionValue.GroupSizes, option.Value).Should().MatchRegex(GroupingShape, option.Value);
            Service.ValueFor(OptionValue.CurrencyGroupSizes, option.Value).Should().MatchRegex(GroupingShape, option.Value);
            Service.ValueFor(OptionValue.CurrencyNegativePattern, option.Value).Should().MatchRegex("^([0-9]|1[0-5])$", option.Value);
            Service.ValueFor(OptionValue.DateOrder, option.Value).Should().MatchRegex("^[0-2]$", option.Value);
            Service.ValueFor(OptionValue.HourClock, option.Value).Should().MatchRegex("^[01]$", option.Value);
            Service.ValueFor(OptionValue.FirstDayOfWeek, option.Value).Should().MatchRegex("^[0-6]$", option.Value);
        }
    }

    [Fact]
    public void SetFor_WithACultureThisMachineDoesNotHave_WritesNothing()
    {
        SetFor(Format, "zz-ZZ").Should().BeNull();
    }

    [Fact]
    public void SetFor_WithSweptCultures_WritesNoPatternCarryingANarrowNoBreakSpace()
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
    public void SetFor_WithAQuotedLiteralInAPattern_WritesItAsItStands()
    {
        // Win32 date pictures copy single-quoted text unchanged; unquoted, the d of the Spanish de is a day token.
        // Windows quotes the spaces around it as well, where ICU quotes the word alone - measured, and another
        // reason the values come from Windows rather than from .NET.
        SetFor(Format, "es-ES")!["sLongDate"].WritePayload.Should().Be("dddd, d' de 'MMMM' de 'yyyy");
    }

    [Fact]
    public void Control_OnTheRegionalFormatSetting_IsKeyedSelection()
    {
        Format.Control.Should().Be(ControlKind.KeyedSelection);
        SetFor(Format, "en-US")!.Keys.Should().BeEquivalentTo(FormatValueNames);
        SetFor(Format, "en-US")!.Keys.Should().BeEquivalentTo(WrittenTargetKeys(Format));
    }

    // Measured 2026-09-21 on Windows 11 24H2: each of these is what Windows
    // itself wrote into Control Panel\International when that format was picked in Settings, and several are
    // values .NET answers differently (it calls the positive sign "+" and the digit substitution "None").
    [Theory]
    [InlineData("en-US", "iDigits", "2")]
    [InlineData("en-US", "iLZero", "1")]
    [InlineData("en-US", "iCountry", "1")]
    [InlineData("en-US", "iPaperSize", "1")]
    [InlineData("en-US", "sLanguage", "ENU")]
    [InlineData("en-US", "sPositiveSign", "")]
    [InlineData("en-US", "NumShape", "1")]
    [InlineData("en-ZA", "iCountry", "27")]
    [InlineData("en-ZA", "iPaperSize", "9")]
    [InlineData("ar-SA", "iCalendarType", "23")]
    [InlineData("ar-SA", "NumShape", "0")]
    [InlineData("ar-SA", "sNativeDigits", "\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669")]
    [InlineData("hi-IN", "sNativeDigits", "\u0966\u0967\u0968\u0969\u096A\u096B\u096C\u096D\u096E\u096F")]
    [InlineData("th-TH", "iCalendarType", "7")]
    public void Windows_own_locale_data_answers_the_nine_values_a_culture_cannot(string key, string name, string expected)
    {
        SetFor(Format, key)![name].WritePayload.Should().Be(expected);
    }

    [Fact]
    public void SetFor_WithACultureWithNoLcid_OmitsLocaleAndKeepsEnUsHexValue()
    {
        var noLcidCulture = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .First(c => c.LCID == TimeRegionLanguageService.NoLcid);

        SetFor(Format, noLcidCulture.Name)!.Keys.Should()
            .BeEquivalentTo(FormatValueNames.Where(name => name != "Locale"));
        SetFor(Format, "en-US")!["Locale"].Should().BeEquivalentTo(StateValue.Of("00000409"));
    }

    [Fact]
    public void Options_ForHomeLocation_OffersEachRegionOnceKeyedByIsoCode()
    {
        var options = Service.Options(HomeLocation, new FakeDetectionContext());

        options.Should().Contain(o => o.Value == "US");
        options.Should().Contain(o => o.Value == "ZA");
        options.Select(o => o.Value).Should().OnlyHaveUniqueItems(
            "there is one United States however many cultures name it");
        options.Single(o => o.Value == "US").Label.Should().Be(new RegionInfo("US").DisplayName);
    }

    [Fact]
    public void Options_ForHomeLocation_DoesNotOfferAnAreaThatIsNotACountry()
    {
        // es-419, en-001 and en-150 build a RegionInfo whose "ISO code" is a UN M49 area number, which has no GeoId.
        var values = Service.Options(HomeLocation, new FakeDetectionContext()).Select(o => o.Value).ToList();

        values.Should().NotContain("001");
        values.Should().NotContain("150");
        values.Should().NotContain("419");
    }

    [Fact]
    public void Options_ForHomeLocation_SortsTheOfferedRegionsByDisplayName()
    {
        var labels = Service.Options(HomeLocation, new FakeDetectionContext()).Select(o => o.Label).ToList();

        labels.Should().BeInAscendingOrder(StringComparer.CurrentCulture);
    }

    [Fact]
    public void CurrentKey_ForHomeLocation_ResolvesTheNationGeoIdBackToItsIsoCode()
    {
        var machine = new FakeDetectionContext().Set(Geo, "Nation", GeoIdOf("ZA"));

        Service.CurrentKey(HomeLocation, machine).Should().Be("ZA");
    }

    [Fact]
    public void CurrentKey_WithAnAmericanGeoId_ReadsBackAsTheUnitedStates()
    {
        // US is the GeoId a region with no geography data falls back to, so it breaks first if the lookup walks cultures.
        var machine = new FakeDetectionContext().Set(Geo, "Nation", GeoIdOf("US"));

        Service.CurrentKey(HomeLocation, machine).Should().Be("US");
    }

    [Fact]
    public void CurrentKey_WithANationNoRegionClaims_FallsBackToTheNameValue()
    {
        var machine = new FakeDetectionContext()
            .Set(Geo, "Nation", "999999")
            .Set(Geo, "Name", "GB");

        Service.CurrentKey(HomeLocation, machine).Should().Be("GB");
    }

    [Fact]
    public void CurrentKey_ForHomeLocationWithNeitherValue_HasNoSelection()
    {
        Service.CurrentKey(HomeLocation, new FakeDetectionContext()).Should().BeNull();
    }

    [Fact]
    public void SetFor_WithARegion_WritesTheGeoIdAndTheIsoCode()
    {
        var set = SetFor(HomeLocation, "US");

        set.Should().NotBeNull();
        set!.Keys.Should().BeEquivalentTo(GeoValueNames);
        set["Name"].WritePayload.Should().Be("US");
        set["Nation"].WritePayload.Should().Be(GeoIdOf("US"));
        set["Nation"].WritePayload!.ToString().Should().MatchRegex("^[0-9]+$", "Nation is a decimal GeoId, not a code");
    }

    [Fact]
    public void SetFor_WithASecondRegion_WritesADifferentGeoId()
    {
        var us = SetFor(HomeLocation, "US")!["Nation"].WritePayload;
        var za = SetFor(HomeLocation, "ZA")!["Nation"].WritePayload;

        za.Should().NotBe(us);
    }

    [Fact]
    public void SetFor_WithARegionWhoseCulturesCarryNoGeography_StillWritesItsOwnGeoId()
    {
        // A RegionInfo built from a culture with no geography data reports the US GeoId; some of Canada's have none.
        var set = SetFor(HomeLocation, "CA");

        set!["Nation"].WritePayload.Should().Be(GeoIdOf("CA"));
        set["Nation"].WritePayload.Should().NotBe(GeoIdOf("US"));
    }

    [Fact]
    public void SetFor_WithAKeyThatIsNotARegion_WritesNothing()
    {
        SetFor(HomeLocation, "ZZ").Should().BeNull();
    }

    [Fact]
    public void Control_OnTheHomeLocationSetting_IsKeyedSelection()
    {
        HomeLocation.Control.Should().Be(ControlKind.KeyedSelection);
        SetFor(HomeLocation, "US")!.Keys.Should().BeEquivalentTo(WrittenTargetKeys(HomeLocation));
    }

    [Fact]
    public void Options_ForKeyboardLayout_SortsLayoutsByLayoutText()
    {
        var options = Service.Options(KeyboardLayout, LayoutMachine());

        options.Select(o => o.Value).Should().BeEquivalentTo(OfferedKlids);
        // Windows NLS and ICU order these labels differently, so the expectation sorts with the service's comparer.
        options.Select(o => o.Label).Should().Equal(
            OfferedLayoutTexts.OrderBy(text => text, StringComparer.CurrentCulture));
    }

    [Fact]
    public void Options_WithTwoLayoutsSharingALayoutText_OrdersThemByKlid()
    {
        var machine = LayoutMachine()
            .Sub(Layouts, "00020409", "00000409")
            .Set($@"{Layouts}\00020409", "Layout Text", "US");

        Service.Options(KeyboardLayout, machine).Select(o => o.Value).Should().Equal("00000409", "00020409");
    }

    [Fact]
    public void Options_WithALayoutWithNoLayoutText_DoesNotOfferIt()
    {
        Service.Options(KeyboardLayout, LayoutMachine()).Should().NotContain(o => o.Value == "0000dead");
    }

    [Fact]
    public void CurrentKey_ForKeyboardLayout_IsTheFirstPreloadEntry()
    {
        var machine = LayoutMachine().Set(Preload, "1", "00000809");

        Service.CurrentKey(KeyboardLayout, machine).Should().Be("00000809");
    }

    [Fact]
    public void CurrentKey_WithASubstitutedPreload_ReportsTheLayoutThatActuallyLoads()
    {
        var machine = LayoutMachine()
            .Set(Preload, "1", "00000409")
            .Set(Substitutes, "00000409", "00010409");

        Service.CurrentKey(KeyboardLayout, machine).Should().Be("00010409");
    }

    // A real machine writes a lowercase KLID into Preload and the same layout in uppercase under Substitutes.
    [Fact]
    public void CurrentKey_WithALowercasePreload_FindsItsUppercaseSubstitute()
    {
        var machine = LayoutMachine()
            .Set(Preload, "1", "00001c09")
            .Set(Substitutes, "00001C09", "00000409");

        Service.CurrentKey(KeyboardLayout, machine).Should().Be("00000409");
    }

    [Fact]
    public void CurrentKey_ForKeyboardLayoutWithNoPreload_HasNoSelection()
    {
        Service.CurrentKey(KeyboardLayout, LayoutMachine()).Should().BeNull();
    }

    [Fact]
    public void SetFor_WithALayout_WritesTheFirstPreloadEntry()
    {
        var set = SetFor(KeyboardLayout, "00000809");

        set.Should().NotBeNull();
        set!.Keys.Should().Equal("Preload1");
        set.Keys.Should().Equal(WrittenTargetKeys(KeyboardLayout));
        set["Preload1"].WritePayload.Should().Be("00000809");
    }

    [Fact]
    public void SetFor_WithASecondLayout_WritesItsOwnKlid()
    {
        var set = SetFor(KeyboardLayout, "00010409");

        set!["Preload1"].WritePayload.Should().Be("00010409");
    }

    [Fact]
    public void SetFor_WithALowercaseKlid_TreatsItAsALayoutId()
    {
        var set = SetFor(KeyboardLayout, "00001c09");

        set!["Preload1"].WritePayload.Should().Be("00001c09");
    }

    [Fact]
    public void SetFor_WithAKeyThatIsNotALayoutId_WritesNothing()
    {
        SetFor(KeyboardLayout, "US").Should().BeNull();
        SetFor(KeyboardLayout, "0409").Should().BeNull();
        SetFor(KeyboardLayout, "zzzzzzzz").Should().BeNull();
        SetFor(KeyboardLayout, "").Should().BeNull();
    }

    [Fact]
    public void EffectFor_WithALayout_RemovesTheSubstituteFiledUnderItsId()
    {
        var removal = Assert.IsType<RegContentEffect>(Service.EffectFor(KeyboardLayout, "00000409"));

        removal.Content.Should().Be(
            $"Windows Registry Editor Version 5.00\r\n\r\n[{Substitutes}]\r\n\"00000409\"=-\r\n");
        ApplyOpScriptEmitter.MixesHives(removal.Content).Should().BeFalse();
    }

    [Fact]
    public void Resolve_WithALayout_EmitsThePreloadWriteThenTheSubstituteRemoval()
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
    public void EffectFor_WithAKeyThatIsNotALayoutId_GivesNoSubstituteRemoval()
    {
        Service.EffectFor(KeyboardLayout, "0409\"=-\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE]").Should().BeNull();
        Service.EffectFor(KeyboardLayout, "zzzzzzzz").Should().BeNull();
    }

    [Fact]
    public void Emit_WithTimeZoneAndKeyboardLayout_RunsTzutilForTheSystemAndTheSubstituteRemovalForTheUser()
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
    public void Control_OnTheKeyboardLayoutSetting_IsKeyedSelection()
    {
        KeyboardLayout.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void Options_ForSystemLocale_OffersEveryCultureWindowsCanSetAsTheSystemLocale()
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
    public void Options_WithACultureWithNoCodePagesOfItsOwn_DoesNotOfferItAsASystemLocale()
    {
        // Hindi is Unicode-only: no ANSI code page at all, and the CP_OEMCP / CP_MACCP sentinels for the other
        // two. Windows leaves such a locale out of the language-for-non-Unicode-programs list.
        var options = Service.Options(SystemLocale, new FakeDetectionContext());

        options.Should().NotContain(o => o.Value == "hi-IN");
        SetFor(SystemLocale, "hi-IN").Should().BeNull();
    }

    [Fact]
    public void Options_ForSystemLocale_SortsTheOfferedLocalesByDisplayName()
    {
        var labels = Service.Options(SystemLocale, new FakeDetectionContext()).Select(o => o.Label).ToList();

        labels.Should().BeInAscendingOrder(StringComparer.CurrentCulture);
    }

    [Fact]
    public void CurrentKey_ForSystemLocale_IsTheHexLcidUnderNlsLanguage()
    {
        var machine = new FakeDetectionContext().Set(Language, "Default", "0409");

        Service.CurrentKey(SystemLocale, machine).Should().Be("en-US");
    }

    [Fact]
    public void CurrentKey_WithALocaleThatIsNotOffered_StillReportsIt()
    {
        var machine = new FakeDetectionContext().Set(Language, "Default", "0439");

        Service.CurrentKey(SystemLocale, machine).Should().Be("hi-IN");
    }

    [Fact]
    public void CurrentKey_WithAHexLcidNoCultureClaims_HasNoSelection()
    {
        var machine = new FakeDetectionContext().Set(Language, "Default", "FFFF");

        Service.CurrentKey(SystemLocale, machine).Should().BeNull();
    }

    [Fact]
    public void CurrentKey_ForSystemLocaleWithNoDefaultValue_HasNoSelection()
    {
        Service.CurrentKey(SystemLocale, new FakeDetectionContext()).Should().BeNull();
    }

    [Theory]
    [InlineData("en-US", "0409", "1252", "437", "10000")]
    [InlineData("ja-JP", "0411", "932", "932", "10001")]
    [InlineData("de-DE", "0407", "1252", "850", "10000")]
    // Basque reports the CP_MACCP sentinel, so MACCP falls back to Mac Roman rather than naming a sentinel.
    [InlineData("eu-ES", "042D", "1252", "850", "10000")]
    public void SetFor_WithACulture_WritesItsLcidAndAllThreeCodePages(
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
    public void SetFor_WithASystemLocaleThisMachineDoesNotHave_WritesNothing()
    {
        SetFor(SystemLocale, "zz-ZZ").Should().BeNull();
    }

    [Fact]
    public void SetFor_WithEveryOfferedSystemLocale_WritesAWellFormedSet()
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
    public void Control_OnTheSystemLocaleSetting_IsKeyedSelection()
    {
        SystemLocale.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void Options_ForShortDate_OffersTheRecommendedPatternsFirst()
    {
        Service.Options(ShortDate, new FakeDetectionContext()).Take(5).Select(option => option.Value)
            .Should().Equal("M/d/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd MMM yyyy");
    }

    // region-format writes dd.MM.yyyy for de-DE and dd-MM-yyyy for hi-IN, which no fixed option list offers.
    [Theory]
    [InlineData("en-US")]
    [InlineData("en-ZA")]
    [InlineData("de-DE")]
    [InlineData("hi-IN")]
    [InlineData("ja-JP")]
    [InlineData("ar-SA")]
    public void Options_ForShortDate_OffersEveryPatternARegionalFormatWrites(string culture)
    {
        var written = SetFor(Format, culture)!["sShortDate"].WritePayload;

        Service.Options(ShortDate, new FakeDetectionContext()).Select(option => option.Value)
            .Should().Contain((string)written!, culture);
    }

    [Fact]
    public void CurrentKey_ForShortDate_IsThePatternTheMachineCarries()
    {
        var machine = new FakeDetectionContext().Set(International, "sShortDate", "dd.MM.yyyy");

        Service.CurrentKey(ShortDate, machine).Should().Be("dd.MM.yyyy");
    }

    [Fact]
    public void SetFor_WithAShortDate_WritesThatPatternAndNothingElse()
    {
        var set = SetFor(ShortDate, "dd.MM.yyyy")!;

        set.Should().ContainKey("sShortDate").WhoseValue.WritePayload.Should().Be("dd.MM.yyyy");
        set.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dd/MM/yyyy\r\nStart-Process calc")]
    public void Accepts_WithAnEmptyPatternOrOneWithControlCharacters_IsFalse(string key) =>
        Service.Accepts(ShortDate, key).Should().BeFalse();

    [Fact]
    public void Options_AfterCallerClearsReturnedList_DoesNotEmptyTheNextOne()
    {
        var first = (List<DynamicOption>)Service.Options(Format, new FakeDetectionContext());
        var count = first.Count;
        first.Clear();

        Service.Options(Format, new FakeDetectionContext()).Should().HaveCount(count);
    }

    [Fact]
    public void Options_AfterUiLanguageSwitch_RelabelsTheListsInThatLanguage()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            Service.Options(Format, new FakeDetectionContext());
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            Service.Options(Format, new FakeDetectionContext()).Single(o => o.Value == "en-US").Label
                .Should().Be(CultureInfo.GetCultureInfo("en-US").DisplayName);
            Service.Options(HomeLocation, new FakeDetectionContext()).Single(o => o.Value == "US").Label
                .Should().Be(new RegionInfo("US").DisplayName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void CurrentKey_OnEveryCall_ReadsTheMachineSettingAgain()
    {
        Service.Options(Format, new FakeDetectionContext());

        Service.CurrentKey(Format, new FakeDetectionContext().Set(International, "LocaleName", "de-DE")).Should().Be("de-DE");
        Service.CurrentKey(Format, new FakeDetectionContext().Set(International, "LocaleName", "en-ZA")).Should().Be("en-ZA");
        Service.CurrentKey(HomeLocation, new FakeDetectionContext().Set(Geo, "Nation", GeoIdOf("ZA"))).Should().Be("ZA");
        Service.CurrentKey(HomeLocation, new FakeDetectionContext().Set(Geo, "Nation", GeoIdOf("US"))).Should().Be("US");
    }
}
