using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Customize.Catalogs;

public static class TimeRegionLanguageCatalog
{
    public const string FeatureId = FeatureIds.TimeRegionLanguage;
    public const string FeatureName = "Time, region and language";

    // Every target is REG_SZ: Windows stores the code pages, the GeoId and iFirstDayOfWeek as strings.
    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "region-time-zone",
            Display = new()
            {
                Name = LocKey.Setting.RegionTimeZone.Name,
                Description = LocKey.Setting.RegionTimeZone.Description,
                GroupName = LocKey.SettingGroup.Time,
                Icon = MaterialIcons.ClockStart,
                AddedInVersion = "26.09.05",
                IsSubjectivePreference = true,
            },
            Options = new(OptionSource.TimeZones) { Path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones", LabelValue = "Display" },
            // The answer file's TimeZone takes the same Windows time-zone id the registry stores.
            Targets = new Target[]
            {
                new RegTarget("TimeZoneKeyName", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation" }, "TimeZoneKeyName", RegistryValueKind.String),
                new AutounattendElement("zone-element", "specialize", "Microsoft-Windows-Shell-Setup", "TimeZone"),
            },
        },
        new()
        {
            Id = "region-format",
            Display = new()
            {
                Name = LocKey.Setting.RegionFormat.Name,
                Description = LocKey.Setting.RegionFormat.Description,
                GroupName = LocKey.SettingGroup.Region,
                Icon = MaterialIcons.Wan,
                AddedInVersion = "26.09.05",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Options = new(OptionSource.RegionalFormats),
            // Windows keeps nine more values here that .NET has no counterpart for in Windows' own numbering (iDigits,
            // iLZero, iCountry, iPaperSize, iCalendarType, sLanguage, sNativeDigits, NumShape, sPositiveSign); they
            // keep the machine's value.
            Targets = new Target[]
            {
                new RegTarget("LocaleName", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "LocaleName", RegistryValueKind.String),
                new RegTarget("Locale", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "Locale", RegistryValueKind.String) { From = OptionValue.LocaleId },
                new RegTarget("sDecimal", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sDecimal", RegistryValueKind.String) { From = OptionValue.DecimalSeparator },
                new RegTarget("sThousand", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sThousand", RegistryValueKind.String) { From = OptionValue.GroupSeparator },
                new RegTarget("sList", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sList", RegistryValueKind.String) { From = OptionValue.ListSeparator },
                new RegTarget("sShortDate", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sShortDate", RegistryValueKind.String) { From = OptionValue.ShortDate },
                new RegTarget("sLongDate", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sLongDate", RegistryValueKind.String) { From = OptionValue.LongDate },
                new RegTarget("sTimeFormat", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sTimeFormat", RegistryValueKind.String) { From = OptionValue.LongTime },
                new RegTarget("sShortTime", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sShortTime", RegistryValueKind.String) { From = OptionValue.ShortTime },
                new RegTarget("iFirstDayOfWeek", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iFirstDayOfWeek", RegistryValueKind.String) { From = OptionValue.FirstDayOfWeek },
                new RegTarget("iMeasure", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iMeasure", RegistryValueKind.String) { From = OptionValue.MeasurementSystem },
                new RegTarget("sMonDecimalSep", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sMonDecimalSep", RegistryValueKind.String) { From = OptionValue.CurrencyDecimalSeparator },
                new RegTarget("sMonThousandSep", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sMonThousandSep", RegistryValueKind.String) { From = OptionValue.CurrencyGroupSeparator },
                new RegTarget("sCurrency", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sCurrency", RegistryValueKind.String) { From = OptionValue.CurrencySymbol },
                new RegTarget("sNegativeSign", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sNegativeSign", RegistryValueKind.String) { From = OptionValue.NegativeSign },
                new RegTarget("sGrouping", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sGrouping", RegistryValueKind.String) { From = OptionValue.GroupSizes },
                new RegTarget("sMonGrouping", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sMonGrouping", RegistryValueKind.String) { From = OptionValue.CurrencyGroupSizes },
                new RegTarget("iCurrDigits", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iCurrDigits", RegistryValueKind.String) { From = OptionValue.CurrencyDecimalDigits },
                new RegTarget("iCurrency", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iCurrency", RegistryValueKind.String) { From = OptionValue.CurrencyPositivePattern },
                new RegTarget("iNegCurr", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iNegCurr", RegistryValueKind.String) { From = OptionValue.CurrencyNegativePattern },
                new RegTarget("iNegNumber", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iNegNumber", RegistryValueKind.String) { From = OptionValue.NumberNegativePattern },
                new RegTarget("s1159", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "s1159", RegistryValueKind.String) { From = OptionValue.AmDesignator },
                new RegTarget("s2359", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "s2359", RegistryValueKind.String) { From = OptionValue.PmDesignator },
                new RegTarget("sDate", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sDate", RegistryValueKind.String) { From = OptionValue.DateSeparator },
                new RegTarget("sTime", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sTime", RegistryValueKind.String) { From = OptionValue.TimeSeparator },
                new RegTarget("sYearMonth", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sYearMonth", RegistryValueKind.String) { From = OptionValue.YearMonth },
                new RegTarget("iDate", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iDate", RegistryValueKind.String) { From = OptionValue.DateOrder },
                new RegTarget("iTime", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iTime", RegistryValueKind.String) { From = OptionValue.HourClock },
                new RegTarget("iTLZero", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iTLZero", RegistryValueKind.String) { From = OptionValue.HourLeadingZero },
                new RegTarget("iTimePrefix", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iTimePrefix", RegistryValueKind.String) { From = OptionValue.TimeMarkerPosition },
                new RegTarget("iFirstWeekOfYear", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iFirstWeekOfYear", RegistryValueKind.String) { From = OptionValue.FirstWeekOfYear },
                new AutounattendElement("format-element", "oobeSystem", "Microsoft-Windows-International-Core", "UserLocale"),
            },
        },
        new()
        {
            Id = "region-home-location",
            Display = new()
            {
                Name = LocKey.Setting.RegionHomeLocation.Name,
                Description = LocKey.Setting.RegionHomeLocation.Description,
                GroupName = LocKey.SettingGroup.Region,
                Icon = MaterialIcons.MapMarker,
                AddedInVersion = "26.09.05",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Options = new(OptionSource.Countries),
            Targets = new Target[]
            {
                new RegTarget("Nation", new[] { @"HKEY_CURRENT_USER\Control Panel\International\Geo" }, "Nation", RegistryValueKind.String) { From = OptionValue.GeoId },
                new RegTarget("Name", new[] { @"HKEY_CURRENT_USER\Control Panel\International\Geo" }, "Name", RegistryValueKind.String),
            },
        },
        new()
        {
            Id = "region-keyboard-layout",
            Display = new()
            {
                Name = LocKey.Setting.RegionKeyboardLayout.Name,
                Description = LocKey.Setting.RegionKeyboardLayout.Description,
                GroupName = LocKey.SettingGroup.LanguageAndKeyboard,
                Icon = MaterialIcons.Keyboard,
                AddedInVersion = "26.09.05",
                IsSubjectivePreference = true,
            },
            Options = new(OptionSource.KeyboardLayouts) { Path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Keyboard Layouts", LabelValue = "Layout Text" },
            Targets = new Target[]
            {
                new RegTarget("Preload1", new[] { @"HKEY_CURRENT_USER\Keyboard Layout\Preload" }, "1", RegistryValueKind.String),
                // The value under this key is named by the Preload id, so the service looks it up per key.
                new RegTarget("Substitutes", new[] { @"HKEY_CURRENT_USER\Keyboard Layout\Substitutes" }, null, RegistryValueKind.String) { ReadOnly = true, ClearedOnApply = true },
            },
        },
        new()
        {
            Id = "region-system-locale",
            Display = new()
            {
                Name = LocKey.Setting.RegionSystemLocale.Name,
                Description = LocKey.Setting.RegionSystemLocale.Description,
                GroupName = LocKey.SettingGroup.LanguageAndKeyboard,
                Icon = MaterialIcons.Translate,
                AddedInVersion = "26.09.05",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Options = new(OptionSource.SystemLocales),
            Targets = new Target[]
            {
                new RegTarget("Default", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Nls\Language" }, "Default", RegistryValueKind.String) { From = OptionValue.SystemLocaleId },
                new RegTarget("ACP", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Nls\CodePage" }, "ACP", RegistryValueKind.String) { From = OptionValue.AnsiCodePage },
                new RegTarget("OEMCP", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Nls\CodePage" }, "OEMCP", RegistryValueKind.String) { From = OptionValue.OemCodePage },
                new RegTarget("MACCP", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Nls\CodePage" }, "MACCP", RegistryValueKind.String) { From = OptionValue.MacCodePage },
                new AutounattendElement("locale-element", "oobeSystem", "Microsoft-Windows-International-Core", "SystemLocale"),
            },
        },
        new()
        {
            Id = "explorer-customization-short-date",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationShortDate.Name,
                Description = LocKey.Setting.ExplorerCustomizationShortDate.Description,
                GroupName = LocKey.SettingGroup.Formats,
                Icon = MaterialIcons.CalendarMonth,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sShortDate", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sShortDate", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortDate.Option0,
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("M/d/yyyy").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortDate.Option1,
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("dd/MM/yyyy") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortDate.Option2,
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("yyyy-MM-dd") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortDate.Option3,
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("yyyy/MM/dd") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortDate.Option4,
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("dd MMM yyyy") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-first-day-of-week",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Name,
                Description = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Description,
                GroupName = LocKey.SettingGroup.Formats,
                Icon = MaterialIcons.CalendarWeekBegin,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("iFirstDayOfWeek", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iFirstDayOfWeek", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("6").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option1,
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("0") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option2,
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option3,
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("2") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option4,
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("3") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option5,
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("4") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationFirstDayOfWeek.Option6,
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("5") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-number-decimal",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNumberDecimal.Name,
                Description = LocKey.Setting.ExplorerCustomizationNumberDecimal.Description,
                GroupName = LocKey.SettingGroup.Formats,
                Icon = MaterialIcons.Numeric,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sDecimal", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sDecimal", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationNumberDecimal.Option0,
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of(".").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationNumberDecimal.Option1,
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of(",") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationNumberDecimal.Option2,
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of(" ") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationNumberDecimal.Option3,
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of("'") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-list-separator",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationListSeparator.Name,
                Description = LocKey.Setting.ExplorerCustomizationListSeparator.Description,
                GroupName = LocKey.SettingGroup.Formats,
                Icon = MaterialIcons.FormatListBulleted,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sList", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sList", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationListSeparator.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["sList"] = Of(",").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationListSeparator.Option1,
                    Set = new Dictionary<string, StateValue> { ["sList"] = Of(";") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-measurement-system",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationMeasurementSystem.Name,
                Description = LocKey.Setting.ExplorerCustomizationMeasurementSystem.Description,
                GroupName = LocKey.SettingGroup.Formats,
                Icon = MaterialIcons.RulerSquare,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("iMeasure", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iMeasure", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationMeasurementSystem.Option0,
                    Set = new Dictionary<string, StateValue> { ["iMeasure"] = Of("0").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationMeasurementSystem.Option1,
                    Set = new Dictionary<string, StateValue> { ["iMeasure"] = Of("1") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-currency-decimal",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationCurrencyDecimal.Name,
                Description = LocKey.Setting.ExplorerCustomizationCurrencyDecimal.Description,
                GroupName = LocKey.SettingGroup.Formats,
                Icon = MaterialIcons.CurrencySign,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sMonDecimalSep", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sMonDecimalSep", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationCurrencyDecimal.Option0,
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of(".").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationCurrencyDecimal.Option1,
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of(",") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationCurrencyDecimal.Option2,
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of(" ") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationCurrencyDecimal.Option3,
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of("'") },
                },
            },
        },
    };
}
