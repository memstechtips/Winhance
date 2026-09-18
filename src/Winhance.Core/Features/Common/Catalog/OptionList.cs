namespace Winhance.Core.Features.Common.Catalog;

public sealed record OptionList(OptionSource Source)
{
    // The registry key whose subkeys are the options.
    public string? Path { get; init; }

    // The value under each option's subkey that captions it.
    public string? LabelValue { get; init; }

    // Options every PC is offered, ahead of whatever this PC adds.
    public IReadOnlyList<string> Keys { get; init; } = [];
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
}

public sealed record DynamicOption(string Label, string Value, bool ExistsOnSystem = true, bool CanDelete = false);
