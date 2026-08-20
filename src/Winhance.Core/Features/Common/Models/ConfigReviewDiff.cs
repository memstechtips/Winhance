namespace Winhance.Core.Features.Common.Models;

public sealed record ConfigReviewDiff
{
    public string SettingId { get; init; } = string.Empty;

    public string SettingName { get; init; } = string.Empty;

    public string FeatureModuleId { get; init; } = string.Empty;

    public string CurrentValueDisplay { get; init; } = string.Empty;

    public string ConfigValueDisplay { get; init; } = string.Empty;

    public ConfigurationItem? ConfigItem { get; init; }

    public bool IsReviewed { get; init; } = false;

    public bool IsApproved { get; init; } = false;

    public bool IsActionSetting { get; init; }

    public string? ActionConfirmationMessage { get; init; }

    public bool IsActionReviewed { get; init; }

    public bool IsActionApproved { get; init; }

    public string? CurrentDisplayKey { get; init; }

    public string? ConfigDisplayKey { get; init; }
}
