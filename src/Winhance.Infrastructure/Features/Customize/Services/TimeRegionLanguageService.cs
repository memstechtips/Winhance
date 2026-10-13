using System.Globalization;
using System.Text;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Windows.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Infrastructure.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Customize.Services;

internal sealed class TimeRegionLanguageService : IOptionProvider
{
    // The LCID .NET reports for a culture Windows has no locale id for.
    internal const int NoLcid = 0x1000;

    private const int RegTziFormatBytes = 44;

    // CP_ACP 0, CP_OEMCP 1, CP_MACCP 2 and CP_THREAD_ACP 3 mean "whatever the system is set to", not a code
    // page. TextInfo reports them for a culture that has no code page of its own.
    private const int HighestCodePageSentinel = 3;

    // Mac Roman, the stand-in for a sentinel Mac code page: MACCP is vestigial on modern Windows.
    private const int MacRomanCodePage = 10000;

    // DYNAMIC_TIME_ZONE_INFORMATION.TimeZoneKeyName holds 128 characters with its terminator.
    private const int MaxTimeZoneIdLength = 127;

    private const string TimeZoneIdPunctuation = " ()+-.";

    private const int MaxLocaleValueLength = 80;

    public IReadOnlyList<OptionSource> Sources { get; } =
    [
        OptionSource.TimeZones,
        OptionSource.KeyboardLayouts,
        OptionSource.RegionalFormats,
        OptionSource.SystemLocales,
        OptionSource.Countries,
        OptionSource.LocaleValues,
        OptionSource.DisplayLanguages,
    ];

    public IReadOnlyList<DynamicOption> Options(Setting setting, IDetectionContext context)
    {
        var list = setting.Options!;
        return list.Source switch
        {
            OptionSource.TimeZones => TimeZones(list, context),
            OptionSource.KeyboardLayouts => KeyboardLayouts(list, context),
            OptionSource.RegionalFormats => CultureOptions(SpecificCultures()),
            OptionSource.SystemLocales => CultureOptions(SystemLocales()),
            OptionSource.Countries => Regions()
                .Select(region => new DynamicOption(region.DisplayName, region.TwoLetterISORegionName))
                .OrderBy(option => option.Label, StringComparer.CurrentCulture)
                .ToList(),
            OptionSource.LocaleValues => LocaleValues(list),
            OptionSource.DisplayLanguages => DisplayLanguages(list, context),
            var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a time, region or language list."),
        };
    }

    public string? CurrentKey(Setting setting, IDetectionContext context) => setting.Options!.Source switch
    {
        OptionSource.TimeZones => NonEmpty(RegTargetReader.Read(setting, "TimeZoneKeyName", context)),
        OptionSource.KeyboardLayouts => CurrentLayout(setting, context),
        OptionSource.RegionalFormats => NonEmpty(RegTargetReader.Read(setting, "LocaleName", context)),
        OptionSource.SystemLocales => CurrentSystemLocale(setting, context),
        OptionSource.Countries => CurrentCountry(setting, context),
        OptionSource.LocaleValues => NonEmpty(WrittenValue(setting, context)),
        // A list with no Path is about media this PC has never seen, so it has no current value to detect.
        OptionSource.DisplayLanguages => setting.Options!.Path is null ? null : CurrentDisplayLanguage(setting, context),
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a time, region or language list."),
    };

    public bool Accepts(Setting setting, string key) => setting.Options!.Source switch
    {
        OptionSource.TimeZones => IsTimeZoneId(key),
        OptionSource.KeyboardLayouts => IsLayoutId(key),
        OptionSource.RegionalFormats => Named(SpecificCultures(), key) is not null,
        OptionSource.SystemLocales => Named(SystemLocales(), key) is not null,
        OptionSource.Countries => RegionNamed(key) is not null,
        // Windows itself writes these, so the set cannot be enumerated ahead of time; only a shape check is possible.
        OptionSource.LocaleValues => key.Length is > 0 and <= MaxLocaleValueLength && !key.Any(char.IsControl),
        // Neutral as well as specific: a language pack may be installed as "en" with no region behind it.
        OptionSource.DisplayLanguages => CultureNamed(key) is not null,
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a time, region or language list."),
    };

    public string? ValueFor(OptionValue value, string key) => value switch
    {
        OptionValue.Key or OptionValue.Color =>
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a time, region or language value."),
        OptionValue.GeoId => RegionNamed(key)?.GeoId.ToString(CultureInfo.InvariantCulture),
        _ when LocaleTypes.TryGetValue(value, out var lcType) => LocaleInfo.Read(key, lcType),
        _ => Named(SpecificCultures(), key) is { } culture ? CultureValue(culture, value) : null,
    };

    // Every value a regional format writes that Windows holds itself. .NET reads ICU, which disagrees with Windows
    // in ways that reach the registry: ICU's ar-SA short date carries right-to-left marks Windows never writes and
    // its Arabic decimal separator where Windows writes a full stop, and ICU says three decimal digits for en-US
    // where Windows stores two. Measured 2026-09-21 on Windows 11 24H2.
    private static readonly Dictionary<OptionValue, uint> LocaleTypes = new()
    {
        [OptionValue.DecimalSeparator] = PInvoke.LOCALE_SDECIMAL,
        [OptionValue.GroupSeparator] = PInvoke.LOCALE_STHOUSAND,
        [OptionValue.ListSeparator] = PInvoke.LOCALE_SLIST,
        [OptionValue.ShortDate] = PInvoke.LOCALE_SSHORTDATE,
        [OptionValue.LongDate] = PInvoke.LOCALE_SLONGDATE,
        [OptionValue.LongTime] = PInvoke.LOCALE_STIMEFORMAT,
        [OptionValue.ShortTime] = PInvoke.LOCALE_SSHORTTIME,
        [OptionValue.FirstDayOfWeek] = PInvoke.LOCALE_IFIRSTDAYOFWEEK,
        [OptionValue.MeasurementSystem] = PInvoke.LOCALE_IMEASURE,
        [OptionValue.CurrencyDecimalSeparator] = PInvoke.LOCALE_SMONDECIMALSEP,
        [OptionValue.CurrencyGroupSeparator] = PInvoke.LOCALE_SMONTHOUSANDSEP,
        [OptionValue.CurrencySymbol] = PInvoke.LOCALE_SCURRENCY,
        [OptionValue.NegativeSign] = PInvoke.LOCALE_SNEGATIVESIGN,
        [OptionValue.GroupSizes] = PInvoke.LOCALE_SGROUPING,
        [OptionValue.CurrencyGroupSizes] = PInvoke.LOCALE_SMONGROUPING,
        [OptionValue.CurrencyDecimalDigits] = PInvoke.LOCALE_ICURRDIGITS,
        [OptionValue.CurrencyPositivePattern] = PInvoke.LOCALE_ICURRENCY,
        [OptionValue.CurrencyNegativePattern] = PInvoke.LOCALE_INEGCURR,
        [OptionValue.NumberNegativePattern] = PInvoke.LOCALE_INEGNUMBER,
        [OptionValue.FirstWeekOfYear] = PInvoke.LOCALE_IFIRSTWEEKOFYEAR,
        [OptionValue.AmDesignator] = PInvoke.LOCALE_S1159,
        [OptionValue.PmDesignator] = PInvoke.LOCALE_S2359,
        [OptionValue.DateSeparator] = PInvoke.LOCALE_SDATE,
        [OptionValue.TimeSeparator] = PInvoke.LOCALE_STIME,
        [OptionValue.YearMonth] = PInvoke.LOCALE_SYEARMONTH,
        [OptionValue.DateOrder] = PInvoke.LOCALE_IDATE,
        [OptionValue.HourClock] = PInvoke.LOCALE_ITIME,
        [OptionValue.HourLeadingZero] = PInvoke.LOCALE_ITLZERO,
        [OptionValue.TimeMarkerPosition] = PInvoke.LOCALE_ITIMEMARKPOSN,
        [OptionValue.DecimalDigits] = PInvoke.LOCALE_IDIGITS,
        [OptionValue.LeadingZero] = PInvoke.LOCALE_ILZERO,
        [OptionValue.CountryCode] = PInvoke.LOCALE_ICOUNTRY,
        [OptionValue.PaperSize] = PInvoke.LOCALE_IPAPERSIZE,
        [OptionValue.CalendarType] = PInvoke.LOCALE_ICALENDARTYPE,
        [OptionValue.AbbreviatedLanguage] = PInvoke.LOCALE_SABBREVLANGNAME,
        [OptionValue.NativeDigits] = PInvoke.LOCALE_SNATIVEDIGITS,
        [OptionValue.DigitSubstitution] = PInvoke.LOCALE_IDIGITSUBSTITUTION,
        [OptionValue.PositiveSign] = PInvoke.LOCALE_SPOSITIVESIGN,
    };

    // Writing TimeZoneKeyName does not switch the zone: the running clock, the Bias/StandardName set and the dynamic
    // DST table stay on the old one. tzutil, which ships with Windows, does, and holds SE_TIME_ZONE_NAME meanwhile.
    public Effect? EffectFor(Setting setting, string key) => setting.Options?.Source switch
    {
        OptionSource.TimeZones when IsTimeZoneId(key) =>
            new ScriptEffect($"tzutil.exe /s '{PowerShellScriptUtilities.EscapePowerShellString(key)}'", RunContext.System),
        OptionSource.KeyboardLayouts when IsLayoutId(key) => new RegContentEffect(SubstituteRemoval(setting, key)),
        OptionSource.DisplayLanguages when setting.Options!.Path is not null && CultureNamed(key) is not null =>
            new RegContentEffect(PreferredUILanguage(setting, key)),
        _ => null,
    };

    // A zone id ends up on tzutil's command line, in the live apply and in the answer-file script, and a config
    // file can name any string. Every id Windows ships is made of these characters, and none of them is a quote.
    private static bool IsTimeZoneId(string key) =>
        key.Length is > 0 and <= MaxTimeZoneIdLength
        && key.All(c => char.IsAsciiLetterOrDigit(c) || TimeZoneIdPunctuation.Contains(c));

    // A KLID is eight hexadecimal digits, the Keyboard Layouts subkey name.
    private static bool IsLayoutId(string key) => key.Length == 8 && key.All(char.IsAsciiHexDigit);

    // Windows loads what Substitutes names for a Preload id in place of that id, so one left under the chosen id
    // would override the layout just written. Value names are not case-sensitive, so either casing of the id finds it.
    private static string SubstituteRemoval(Setting setting, string klid)
    {
        string substitutes = RegTargetReader.Target(setting, "Substitutes").Paths[0];
        return $"Windows Registry Editor Version 5.00\r\n\r\n[{substitutes}]\r\n\"{klid}\"=-\r\n";
    }

    // The card's Keys first, because they are the formats Winhance recommends and their order is deliberate, then
    // every other value the offered regional formats write, read the way region-format itself reads them. Built
    // this way the card always has an entry for whatever region-format just wrote, which is what stops it reading
    // "Not recognized" after a format apply.
    private List<DynamicOption> LocaleValues(OptionList list) =>
        list.Keys
            .Concat(SpecificCultures()
                .Select(culture => ValueFor(list.Value!.Value, culture.Name))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .OrderBy(value => value, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Select(value => new DynamicOption(value, value))
            .ToList();

    private static string? WrittenValue(Setting setting, IDetectionContext context) =>
        RegTargetReader.Read(setting, setting.Targets.OfType<RegTarget>().First(t => !t.ReadOnly).Key, context) as string;

    // The five values Windows has no locale type for: an LCID in the shape each key wants, and the code pages the
    // system locale writes. Everything a regional format writes comes from Windows itself through LocaleTypes.
    private static string? CultureValue(CultureInfo culture, OptionValue value)
    {
        var text = culture.TextInfo;

        return value switch
        {
            OptionValue.LocaleId => culture.LCID == NoLcid ? null : culture.LCID.ToString("X8", CultureInfo.InvariantCulture),
            // Nls\Language\Default is the LCID as four hex digits, not as a number.
            OptionValue.SystemLocaleId => culture.LCID.ToString("X4", CultureInfo.InvariantCulture),
            OptionValue.AnsiCodePage => CodePage(text.ANSICodePage),
            OptionValue.OemCodePage => CodePage(text.OEMCodePage),
            OptionValue.MacCodePage => CodePage(text.MacCodePage > HighestCodePageSentinel ? text.MacCodePage : MacRomanCodePage),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Not a culture value."),
        };
    }

    private static List<DynamicOption> TimeZones(OptionList list, IDetectionContext context)
    {
        var zones = new List<(DynamicOption Option, int? Bias)>();
        foreach (var zone in context.GetSubKeyNames(list.Path!))
        {
            var zoneKey = $@"{list.Path}\{zone}";
            // Windows localizes the caption, so it is the label.
            if (context.GetValue(zoneKey, list.LabelValue) is string display && display.Length > 0)
                zones.Add((new DynamicOption(display, zone), BiasOf(context.GetValue(zoneKey, "TZI") as byte[])));
        }

        zones.Sort(ByOffsetThenName);
        return zones.Select(zone => zone.Option).ToList();
    }

    // Windows Settings lists zones by UTC offset. REG_TZI_FORMAT opens with a LONG Bias in minutes where
    // UTC = local + Bias, so ascending offset is descending Bias.
    private static int ByOffsetThenName(
        (DynamicOption Option, int? Bias) left, (DynamicOption Option, int? Bias) right)
    {
        if (left.Bias is { } leftBias && right.Bias is { } rightBias && leftBias != rightBias)
            return rightBias.CompareTo(leftBias);
        if (left.Bias.HasValue != right.Bias.HasValue)
            return left.Bias.HasValue ? -1 : 1;
        return StringComparer.Ordinal.Compare(left.Option.Label, right.Option.Label);
    }

    private static int? BiasOf(byte[]? tzi) =>
        tzi is { Length: >= RegTziFormatBytes } ? BitConverter.ToInt32(tzi, 0) : null;

    private static List<DynamicOption> KeyboardLayouts(OptionList list, IDetectionContext context)
    {
        var options = new List<DynamicOption>();
        foreach (var klid in context.GetSubKeyNames(list.Path!))
        {
            if (context.GetValue($@"{list.Path}\{klid}", list.LabelValue) is string text && text.Length > 0)
                options.Add(new DynamicOption(text, klid));
        }

        // Several KLIDs can carry one caption, so the id breaks the tie.
        return options
            .OrderBy(option => option.Label, StringComparer.CurrentCulture)
            .ThenBy(option => option.Value, StringComparer.Ordinal)
            .ToList();
    }

    // A KLID listed under Substitutes is swapped for another, and the substitute is what the machine loads.
    private static string? CurrentLayout(Setting setting, IDetectionContext context)
    {
        if (NonEmpty(RegTargetReader.Read(setting, "Preload1", context)) is not { } preload)
            return null;

        return NonEmpty(context.GetValue(RegTargetReader.Target(setting, "Substitutes").Paths[0], preload)) ?? preload;
    }

    // Matched against every culture, not only the offered ones: the card has a state for a key outside its options.
    private static string? CurrentSystemLocale(Setting setting, IDetectionContext context)
    {
        if (RegTargetReader.Read(setting, "Default", context) is not string raw
            || !int.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lcid))
        {
            return null;
        }

        return SpecificCultures().FirstOrDefault(culture => culture.LCID != NoLcid && culture.LCID == lcid)?.Name;
    }

    private static string? CurrentCountry(Setting setting, IDetectionContext context)
    {
        if (RegTargetReader.Read(setting, "Nation", context) is { } nation
            && int.TryParse(nation.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int geoId)
            && Regions().FirstOrDefault(region => region.GeoId == geoId) is { } byGeoId)
        {
            return byGeoId.TwoLetterISORegionName;
        }

        // Name holds the same answer as an ISO code, and stands in when .NET has no RegionInfo for the GeoId.
        return NonEmpty(RegTargetReader.Read(setting, "Name", context));
    }

    private static List<DynamicOption> CultureOptions(IEnumerable<CultureInfo> cultures) =>
        cultures
            .Select(culture => new DynamicOption(culture.DisplayName, culture.Name))
            .OrderBy(option => option.Label, StringComparer.CurrentCulture)
            .ToList();

    private static CultureInfo? Named(IEnumerable<CultureInfo> cultures, string key) =>
        cultures.FirstOrDefault(culture => string.Equals(culture.Name, key, StringComparison.OrdinalIgnoreCase));

    private static RegionInfo? RegionNamed(string key) =>
        Regions().FirstOrDefault(region => string.Equals(region.TwoLetterISORegionName, key, StringComparison.OrdinalIgnoreCase));

    // Never cached: a static list would freeze the display names in the UI language the process started in.
    private static IEnumerable<CultureInfo> SpecificCultures() =>
        CultureInfo.GetCultures(CultureTypes.SpecificCultures);

    // Windows offers a locale for non-Unicode programs only when it has real code pages behind ACP and OEMCP.
    // A system locale is an LCID, so a culture Windows has no locale id for is not offered either.
    private static IEnumerable<CultureInfo> SystemLocales() =>
        SpecificCultures().Where(culture =>
            culture.LCID != NoLcid
            && culture.TextInfo.ANSICodePage > HighestCodePageSentinel
            && culture.TextInfo.OEMCodePage > HighestCodePageSentinel);

    // One region per ISO code, rebuilt from the code: a RegionInfo built from a culture name reports the United
    // States GeoId when the machine has no geography for that culture, so the first one found would write 244 for
    // CA. Ordered by code so that two regions sharing a GeoId resolve the same way on every call.
    private static IEnumerable<RegionInfo> Regions()
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var culture in SpecificCultures())
        {
            if (RegionOf(culture) is { } region && IsCountryCode(region.TwoLetterISORegionName))
                codes.Add(region.TwoLetterISORegionName);
        }

        foreach (var code in codes)
        {
            if (RegionOfCode(code) is { } region)
                yield return region;
        }
    }

    // ICU names UN M49 areas like countries (001 World, 419 Latin America); Windows has no home location for an area.
    private static bool IsCountryCode(string code) => code.Length == 2 && code.All(char.IsAsciiLetterUpper);

    private static RegionInfo? RegionOf(CultureInfo culture) => RegionOfCode(culture.Name);

    // ICU ships locales and codes Windows has no country for (Diego Garcia, Ceuta and Melilla); RegionInfo throws.
    private static RegionInfo? RegionOfCode(string name)
    {
        try
        {
            return new RegionInfo(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string CodePage(int codePage) => codePage.ToString(CultureInfo.InvariantCulture);

    // With a Path, the packs this PC actually has; without one, every language .NET knows, because the answer
    // file is for installation media this PC has never seen.
    private static List<DynamicOption> DisplayLanguages(OptionList list, IDetectionContext context)
    {
        if (list.Path is null)
            return CultureOptions(SpecificCultures());

        var installed = new List<DynamicOption>();
        foreach (var name in context.GetSubKeyNames(list.Path))
            if (CultureNamed(name) is { } culture)
                installed.Add(new DynamicOption(culture.DisplayName, culture.Name));

        installed.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.CurrentCulture));
        return installed;
    }

    // PreferredUILanguages is REG_MULTI_SZ and the display language is its first entry; Windows appends the
    // fallbacks behind it. A machine that has never been switched has no value at all.
    private static string? CurrentDisplayLanguage(Setting setting, IDetectionContext context) =>
        RegTargetReader.Read(setting, "PreferredUILanguages", context) switch
        {
            string[] { Length: > 0 } list => CultureNamed(list[0])?.Name,
            string single => CultureNamed(single)?.Name,
            _ => null,
        };

    // REG_MULTI_SZ has no .reg text form, so the pair Windows reads is written as hex(7): UTF-16LE, one
    // terminator for the entry and a second for the list.
    private static string PreferredUILanguage(Setting setting, string key)
    {
        string path = RegTargetReader.Target(setting, "PreferredUILanguages").Paths[0];
        var bytes = Encoding.Unicode.GetBytes(key + "\0\0");
        var hex = string.Join(",", bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        return $"Windows Registry Editor Version 5.00\r\n\r\n[{path}]\r\n\"PreferredUILanguages\"=hex(7):{hex}\r\n";
    }

    // A MUI subkey is named by its language tag, and some images name it by its hexadecimal LCID instead, so a
    // name .NET does not know as a tag is tried as one before it is dropped rather than assuming either shape.
    private static CultureInfo? CultureNamed(string name)
    {
        if (name.Length == 0)
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
        }

        if (!int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lcid) || lcid == NoLcid)
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(lcid);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static string? NonEmpty(object? value) => value is string text && text.Length > 0 ? text : null;
}
