using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winhance.UI.Features.Common.Models;

public enum DetailRowType
{
    Registry,
    ScheduledTask,
    PowerConfig,
    PowerShellScript,
    RegContent,
    Dependency,
    // One option of a Selection/Toggle setting: which choice writes which value (the "docs inside the app"
    // option->value table). Sourced from Setting.States.
    Option,
    // A generic two-part docs line (primary + secondary text). Used by the Targets,
    // Effects, and Relationships sections so they share one DataTemplate.
    Info
}

public class TechnicalDetailRow
{
    public DetailRowType RowType { get; set; }

    // Registry fields
    public string RegistryPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string RecommendedValue { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;

    // Action ("On Apply") row: one-shot Action settings have no Recommended/Default state,
    // so those columns are hidden and we instead show what clicking the button writes.
    public bool IsActionRow { get; set; }
    public string OnApplyValue { get; set; } = string.Empty;
    public string OnApplyLabel { get; set; } = "On Apply";

    // ScheduledTask fields
    public string TaskPath { get; set; } = string.Empty;
    public string RecommendedState { get; set; } = string.Empty;
    public string DefaultState { get; set; } = string.Empty;
    public string CurrentState { get; set; } = string.Empty;

    // PowerConfig fields
    public string SubgroupGuid { get; set; } = string.Empty;
    public string SettingGuid { get; set; } = string.Empty;
    public string SubgroupAlias { get; set; } = string.Empty;
    public string SettingAlias { get; set; } = string.Empty;
    public string PowerUnits { get; set; } = string.Empty;
    public string RecommendedAC { get; set; } = string.Empty;
    public string RecommendedDC { get; set; } = string.Empty;

    // PowerConfig — Current and Default AC/DC
    public string CurrentAC { get; set; } = string.Empty;
    public string CurrentDC { get; set; } = string.Empty;
    public string DefaultAC { get; set; } = string.Empty;
    public string DefaultDC { get; set; } = string.Empty;

    // PowerShell Script
    public string ScriptLabel { get; set; } = string.Empty;
    public string ScriptBody { get; set; } = string.Empty;

    // RegContent
    public string ContentLabel { get; set; } = string.Empty;
    public string ContentBody { get; set; } = string.Empty;

    // Dependency
    public string DependencyLabel { get; set; } = string.Empty;
    public string DependencyRelation { get; set; } = string.Empty;

    // Option (Selection/Toggle option->value table). OptionLabel = the localized choice name;
    // OptionValue = the formatted value that choice writes (with "or not set" / "deletes key" suffixes);
    // OptionRole = the localized role marker ("Recommended" / "Default" / both, or empty);
    // IsCurrentOption highlights the choice currently active on the system.
    public string OptionLabel { get; set; } = string.Empty;
    public string OptionValue { get; set; } = string.Empty;
    public string OptionRole { get; set; } = string.Empty;
    public bool IsCurrentOption { get; set; }
    public string CurrentLabelText { get; set; } = string.Empty;

    // Info (Targets / Effects / Relationships): a primary label + a secondary detail string.
    public string InfoPrimary { get; set; } = string.Empty;
    public string InfoSecondary { get; set; } = string.Empty;

    // Localized labels for XAML binding
    public string PathLabel { get; set; } = "Path";
    public string ValueLabel { get; set; } = "Value";
    public string CurrentLabel { get; set; } = "Current";
    public string RecommendedLabel { get; set; } = "Recommended";
    public string DefaultLabel { get; set; } = "Default";
    public string SubgroupLabel { get; set; } = "Subgroup";
    public string SettingLabel { get; set; } = "Setting";

    // Computed bools for XAML visibility
    public bool IsRegistry => RowType == DetailRowType.Registry;
    public bool IsScheduledTask => RowType == DetailRowType.ScheduledTask;
    public bool IsPowerConfig => RowType == DetailRowType.PowerConfig;
    public bool IsPowerShellScript => RowType == DetailRowType.PowerShellScript;
    public bool IsRegContent       => RowType == DetailRowType.RegContent;
    public bool IsDependency       => RowType == DetailRowType.Dependency;
    public bool IsOption           => RowType == DetailRowType.Option;
    public bool IsInfo             => RowType == DetailRowType.Info;

    // Command and icon set from parent ViewModel
    public IRelayCommand<string>? OpenRegeditCommand { get; set; }
    public SoftwareBitmapSource? RegeditIconSource { get; set; }

    /// <summary>
    /// False when the registry key path does not exist, disabling the regedit button.
    /// </summary>
    public bool CanOpenRegedit { get; set; } = true;

    /// <summary>
    /// Returns a concatenated text summary of this row for screen reader accessibility.
    /// </summary>
    public string AccessibleSummary => RowType switch
    {
        DetailRowType.Registry => IsActionRow
            ? $"Registry. Path: {RegistryPath}, Value: {ValueName} ({ValueType}), Current: {CurrentValue}, {OnApplyLabel}: {OnApplyValue}"
            : $"Registry. Path: {RegistryPath}, Value: {ValueName} ({ValueType}), Current: {CurrentValue}, Recommended: {RecommendedValue}, Default: {DefaultValue}",
        DetailRowType.ScheduledTask => string.IsNullOrEmpty(DefaultState)
            ? $"Scheduled Task. TaskPath: {TaskPath}, Recommended: {RecommendedState}"
            : $"Scheduled Task. TaskPath: {TaskPath}, Recommended: {RecommendedState}, Default: {DefaultState}",
        DetailRowType.PowerConfig =>
            $"Power Config. Subgroup: {SubgroupAlias} ({SubgroupGuid}), Setting: {SettingAlias} ({SettingGuid}), AC: {RecommendedAC}, DC: {RecommendedDC}, {PowerUnits}",
        DetailRowType.PowerShellScript => $"PowerShell script {ScriptLabel}: {ScriptBody}",
        DetailRowType.RegContent       => $"Registry content {ContentLabel}: {ContentBody}",
        DetailRowType.Dependency       => $"Depends on {DependencyLabel} {DependencyRelation}",
        DetailRowType.Option           => string.IsNullOrEmpty(OptionRole)
            ? $"{OptionLabel}: {OptionValue}{(IsCurrentOption ? $", {CurrentLabelText}" : string.Empty)}"
            : $"{OptionLabel}: {OptionValue} ({OptionRole}){(IsCurrentOption ? $", {CurrentLabelText}" : string.Empty)}",
        DetailRowType.Info             => string.IsNullOrEmpty(InfoSecondary)
            ? InfoPrimary
            : $"{InfoPrimary}: {InfoSecondary}",
        _ => string.Empty
    };
}
