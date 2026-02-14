using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winhance.UI.Features.Common.Models;

public enum DetailRowType
{
    Registry,
    ScheduledTask,
    PowerConfig
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

    // ScheduledTask fields
    public string TaskPath { get; set; } = string.Empty;
    public string RecommendedState { get; set; } = string.Empty;

    // PowerConfig fields
    public string SubgroupGuid { get; set; } = string.Empty;
    public string SettingGuid { get; set; } = string.Empty;
    public string SubgroupAlias { get; set; } = string.Empty;
    public string SettingAlias { get; set; } = string.Empty;
    public string PowerUnits { get; set; } = string.Empty;
    public string RecommendedAC { get; set; } = string.Empty;
    public string RecommendedDC { get; set; } = string.Empty;

    // Computed bools for XAML visibility
    public bool IsRegistry => RowType == DetailRowType.Registry;
    public bool IsScheduledTask => RowType == DetailRowType.ScheduledTask;
    public bool IsPowerConfig => RowType == DetailRowType.PowerConfig;

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
            $"Registry. Path: {RegistryPath}, Value: {ValueName} ({ValueType}), Current: {CurrentValue}, Recommended: {RecommendedValue}",
        DetailRowType.ScheduledTask =>
            $"Scheduled Task. TaskPath: {TaskPath}, Recommended: {RecommendedState}",
        DetailRowType.PowerConfig =>
            $"Power Config. Subgroup: {SubgroupAlias} ({SubgroupGuid}), Setting: {SettingAlias} ({SettingGuid}), AC: {RecommendedAC}, DC: {RecommendedDC}, {PowerUnits}",
        _ => string.Empty
    };
}
