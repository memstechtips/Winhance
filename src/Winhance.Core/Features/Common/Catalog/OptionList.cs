using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record OptionList(OptionSource Source)
{
    // The registry key whose subkeys are the options.
    public string? Path { get; init; }

    // The value under each option's subkey that captions it.
    public string? LabelValue { get; init; }

    // Options every PC is offered, ahead of whatever this PC adds.
    public IReadOnlyList<string> Keys { get; init; } = [];

    // Which locale value a LocaleValues list offers, e.g. ShortDate for the short-date patterns.
    public OptionValue? Value { get; init; }

    // Captions an entry carrying the empty key, offered ahead of every real option. The writers read an empty
    // key as "leave this out of the file", which Setup reads as a question to ask.
    public LocKey? NoChoiceLabel { get; init; }

    // Shown as a warning, with the card disabled, when this list offers the user nothing to choose between.
    public LocKey? SingleOptionWarning { get; init; }
}

public enum OptionSource
{
    PowerPlans,
    TimeZones,
    KeyboardLayouts,
    RegionalFormats,
    SystemLocales,
    Countries,
    Pictures,
    Colors,

    // The display languages installed on this PC when the list names a registry Path, every language .NET knows
    // when it does not: one card is about this machine, the other about media it has never seen.
    DisplayLanguages,

    // Every distinct value the offered regional formats write for one locale value. A card keyed on a value the
    // region-format card also writes has to offer all of them, or choosing a format leaves it "Not recognized".
    LocaleValues,
}

public enum OptionValue
{
    Key,
    Color,
    LocaleId,
    DecimalSeparator,
    GroupSeparator,
    ListSeparator,
    ShortDate,
    LongDate,
    LongTime,
    ShortTime,
    FirstDayOfWeek,
    MeasurementSystem,
    CurrencyDecimalSeparator,
    CurrencyGroupSeparator,
    CurrencySymbol,
    NegativeSign,
    GroupSizes,
    CurrencyGroupSizes,
    CurrencyDecimalDigits,
    CurrencyPositivePattern,
    CurrencyNegativePattern,
    NumberNegativePattern,
    AmDesignator,
    PmDesignator,
    DateSeparator,
    TimeSeparator,
    YearMonth,
    DateOrder,
    HourClock,
    HourLeadingZero,
    TimeMarkerPosition,
    FirstWeekOfYear,
    SystemLocaleId,
    AnsiCodePage,
    OemCodePage,
    MacCodePage,
    GeoId,

    // Nine a regional format keeps that .NET cannot answer in Windows' own numbering, so they come from
    // Windows' locale data instead: iDigits, iLZero, iCountry, iPaperSize, iCalendarType, sLanguage,
    // sNativeDigits, NumShape, sPositiveSign.
    DecimalDigits,
    LeadingZero,
    CountryCode,
    PaperSize,
    CalendarType,
    AbbreviatedLanguage,
    NativeDigits,
    DigitSubstitution,
    PositiveSign,
}

public sealed record DynamicOption(string Label, string Value, bool ExistsOnSystem = true, bool CanDelete = false);
