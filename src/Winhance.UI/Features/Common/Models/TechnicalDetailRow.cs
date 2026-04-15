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
    Dependency
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

    // ScheduledTask fields
    public string TaskPath { get; set; } = string.Empty;
    public string RecommendedState { get; set; } = string.Empty;
    public string DefaultState { get; set; } = string.Empty;

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

    // Localized labels for XAML binding
    public string PathLabel { get; set; } = "Path";
    public string ValueLabel { get; set; } = "Value";
    public string CurrentLabel { get; set; } = "Current";
    public string RecommendedLabel { get; set; } = "Recommended";
    public string DefaultLabel { get; set; } = "Default";

    // Computed bools for XAML visibility
    public bool IsRegistry => RowType == DetailRowType.Registry;
    public bool IsScheduledTask => RowType == DetailRowType.ScheduledTask;
    public bool IsPowerConfig => RowType == DetailRowType.PowerConfig;
    public bool IsPowerShellScript => RowType == DetailRowType.PowerShellScript;
    public bool IsRegContent       => RowType == DetailRowType.RegContent;
    public bool IsDependency       => RowType == DetailRowType.Dependency;

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
        DetailRowType.Registry =>
            $"Registry. Path: {RegistryPath}, Value: {ValueName} ({ValueType}), Current: {CurrentValue}, Recommended: {RecommendedValue}, Default: {DefaultValue}",
        DetailRowType.ScheduledTask => string.IsNullOrEmpty(DefaultState)
            ? $"Scheduled Task. TaskPath: {TaskPath}, Recommended: {RecommendedState}"
            : $"Scheduled Task. TaskPath: {TaskPath}, Recommended: {RecommendedState}, Default: {DefaultState}",
        DetailRowType.PowerConfig =>
            $"Power Config. Subgroup: {SubgroupAlias} ({SubgroupGuid}), Setting: {SettingAlias} ({SettingGuid}), AC: {RecommendedAC}, DC: {RecommendedDC}, {PowerUnits}",
        DetailRowType.PowerShellScript => $"PowerShell script {ScriptLabel}: {ScriptBody}",
        DetailRowType.RegContent       => $"Registry content {ContentLabel}: {ContentBody}",
        DetailRowType.Dependency       => $"Depends on {DependencyLabel} {DependencyRelation}",
        _ => string.Empty
    };
}
