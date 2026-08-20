using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

public record SettingItemViewModelConfig
{
    public required Setting Setting { get; init; }

    // Read once per load; threaded onto the VM so build-aware default/badge resolution (a merged Selection's
    // OS-divergent WindowsDefault) picks the state that is default on THIS OS. Build 0 (Windows 10 range) when unset.
    public WinBuild Build { get; init; }

    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; init; }
    public required string SettingId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string IconPack { get; init; } = "Material";
    public required InputType InputType { get; init; }
    public bool IsSelected { get; init; }

    public SettingDetectionOutcome Outcome { get; init; }
    public string OnText { get; init; } = "On";
    public string OffText { get; init; } = "Off";
    public string ActionButtonText { get; init; } = "Apply";

    // Index-aligned with the options; null entries = no warning.
    public IReadOnlyList<string?>? OptionWarnings { get; init; }
}
