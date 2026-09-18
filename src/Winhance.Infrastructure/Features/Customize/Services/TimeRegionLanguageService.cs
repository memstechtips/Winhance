using System.Globalization;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Autounattend.Helpers;

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

    // LOCALE_INEGCURR stops at 15; .NET has a pattern 16.
    private const int HighestNegativeCurrencyFormat = 15;

    public IReadOnlyList<OptionSource> Sources { get; } =
    [
        OptionSource.TimeZones,
        OptionSource.KeyboardLayouts,
        OptionSource.RegionalFormats,
        OptionSource.SystemLocales,
        OptionSource.Countries,
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
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a time, region or language list."),
    };

    public bool Accepts(Setting setting, string key) => setting.Options!.Source switch
    {
        OptionSource.TimeZones => IsTimeZoneId(key),
        OptionSource.KeyboardLayouts => IsLayoutId(key),
        OptionSource.RegionalFormats => Named(SpecificCultures(), key) is not null,
        OptionSource.SystemLocales => Named(SystemLocales(), key) is not null,
        OptionSource.Countries => RegionNamed(key) is not null,
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a time, region or language list."),
    };

    public string? ValueFor(OptionValue value, string key) => value switch
    {
        OptionValue.Key or OptionValue.Color =>
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a time, region or language value."),
        OptionValue.GeoId => RegionNamed(key)?.GeoId.ToString(CultureInfo.InvariantCulture),
        _ => Named(SpecificCultures(), key) is { } culture ? CultureValue(culture, value) : null,
    };

    // Writing TimeZoneKeyName does not switch the zone: the running clock, the Bias/StandardName set and the dynamic
    // DST table stay on the old one. tzutil, which ships with Windows, does, and holds SE_TIME_ZONE_NAME meanwhile.
    public Effect? EffectFor(Setting setting, string key) => setting.Options?.Source switch
    {
        OptionSource.TimeZones when IsTimeZoneId(key) =>
            new ScriptEffect($"tzutil.exe /s '{PowerShellScriptUtilities.EscapePowerShellString(key)}'", RunContext.System),
        OptionSource.KeyboardLayouts when IsLayoutId(key) => new RegContentEffect(SubstituteRemoval(setting, key)),
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

    private static string? CultureValue(CultureInfo culture, OptionValue value)
    {
        var date = culture.DateTimeFormat;
        var number = culture.NumberFormat;
        var text = culture.TextInfo;

        return value switch
        {
            OptionValue.LocaleId => culture.LCID == NoLcid ? null : culture.LCID.ToString("X8", CultureInfo.InvariantCulture),
            OptionValue.DecimalSeparator => number.NumberDecimalSeparator,
            OptionValue.GroupSeparator => number.NumberGroupSeparator,
            OptionValue.ListSeparator => text.ListSeparator,
            OptionValue.ShortDate => WindowsPattern(date.ShortDatePattern),
            OptionValue.LongDate => WindowsPattern(date.LongDatePattern),
            OptionValue.LongTime => WindowsPattern(date.LongTimePattern),
            OptionValue.ShortTime => WindowsPattern(date.ShortTimePattern),
            // Windows counts the week from Monday (0) to Sunday (6); System.DayOfWeek counts from Sunday (0).
            OptionValue.FirstDayOfWeek => (((int)date.FirstDayOfWeek + 6) % 7).ToString(CultureInfo.InvariantCulture),
            OptionValue.MeasurementSystem => RegionOf(culture) is { } region ? (region.IsMetric ? "0" : "1") : null,
            OptionValue.CurrencyDecimalSeparator => number.CurrencyDecimalSeparator,
            OptionValue.CurrencyGroupSeparator => number.CurrencyGroupSeparator,
            OptionValue.CurrencySymbol => number.CurrencySymbol,
            OptionValue.NegativeSign => number.NegativeSign,
            OptionValue.GroupSizes => WindowsGrouping(number.NumberGroupSizes),
            OptionValue.CurrencyGroupSizes => WindowsGrouping(number.CurrencyGroupSizes),
            OptionValue.CurrencyDecimalDigits => number.CurrencyDecimalDigits.ToString(CultureInfo.InvariantCulture),
            // .NET numbers these patterns, and CalendarWeekRule, the way LOCALE_ICURRENCY, LOCALE_INEGCURR,
            // LOCALE_INEGNUMBER and LOCALE_IFIRSTWEEKOFYEAR number theirs.
            OptionValue.CurrencyPositivePattern => number.CurrencyPositivePattern.ToString(CultureInfo.InvariantCulture),
            OptionValue.CurrencyNegativePattern => number.CurrencyNegativePattern <= HighestNegativeCurrencyFormat
                ? number.CurrencyNegativePattern.ToString(CultureInfo.InvariantCulture)
                : null,
            OptionValue.NumberNegativePattern => number.NumberNegativePattern.ToString(CultureInfo.InvariantCulture),
            OptionValue.FirstWeekOfYear => ((int)date.CalendarWeekRule).ToString(CultureInfo.InvariantCulture),
            OptionValue.AmDesignator => WindowsPattern(date.AMDesignator),
            OptionValue.PmDesignator => WindowsPattern(date.PMDesignator),
            OptionValue.DateSeparator => date.DateSeparator,
            OptionValue.TimeSeparator => date.TimeSeparator,
            OptionValue.YearMonth => WindowsPattern(date.YearMonthPattern),
            // Windows keeps these four beside the pictures they restate, for programs older than the pictures.
            OptionValue.DateOrder => DateOrderOf(Unquoted(date.ShortDatePattern)),
            OptionValue.HourClock => Unquoted(date.LongTimePattern).Contains('H') ? "1" : "0",
            OptionValue.HourLeadingZero => HourLeadingZeroOf(Unquoted(date.LongTimePattern)),
            OptionValue.TimeMarkerPosition => TimeMarkerPositionOf(Unquoted(date.LongTimePattern)),
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

    // ICU puts a narrow no-break space before the AM/PM designator, and Win32 reads a character it does not know as
    // a literal, so it would show in the clock. Single-quoted runs stay: Win32 pictures copy quoted text unchanged too.
    private static string WindowsPattern(string pattern) =>
        pattern.Replace("\u202f", " ", StringComparison.Ordinal)
            .Replace("\u00a0", " ", StringComparison.Ordinal);

    // LOCALE_SGROUPING ends in ;0 when the last size repeats. .NET repeats the last size unless its array ends in 0.
    internal static string WindowsGrouping(int[] sizes)
    {
        if (sizes.Length == 0 || sizes[0] == 0)
            return "0";

        return sizes[^1] == 0
            ? string.Join(';', sizes[..^1])
            : string.Join(';', sizes) + ";0";
    }

    // Win32 and .NET both copy single-quoted text in a date or time picture out unread.
    internal static string Unquoted(string pattern) =>
        string.Concat(pattern.Split('\'').Where((_, index) => index % 2 == 0));

    // LOCALE_IDATE: 0 month-day-year, 1 day-month-year, 2 year-month-day.
    internal static string? DateOrderOf(string picture) =>
        picture.FirstOrDefault(c => c is 'M' or 'd' or 'y') switch { 'M' => "0", 'd' => "1", 'y' => "2", _ => null };

    internal static string HourLeadingZeroOf(string clock) =>
        clock.Contains("HH", StringComparison.Ordinal) || clock.Contains("hh", StringComparison.Ordinal) ? "1" : "0";

    // LOCALE_ITIMEMARKPOSN: 1 when the AM/PM marker leads the hour, as in Korean.
    internal static string TimeMarkerPositionOf(string clock)
    {
        int marker = clock.IndexOf('t');
        int hour = clock.IndexOf("h", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 && marker < hour ? "1" : "0";
    }

    private static string CodePage(int codePage) => codePage.ToString(CultureInfo.InvariantCulture);

    private static string? NonEmpty(object? value) => value is string text && text.Length > 0 ? text : null;
}
