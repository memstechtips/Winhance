using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Captures all initialization data for a SettingItemViewModel, replacing
/// the object initializer pattern in SettingViewModelFactory.CreateAsync().
/// </summary>
public record SettingItemViewModelConfig
{
    public required Setting Setting { get; init; }

    /// <summary>The live Windows build, read once per load in SettingsLoadingService. Threaded onto the VM so its
    /// build-aware default/badge resolution (a merged Selection's OS-divergent WindowsDefault, e.g. theme-mode-windows)
    /// picks the state that is default on THIS OS. Defaults to build 0 (Windows 10 range) when unset.</summary>
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

    /// <summary>Whether detection placed the setting on a known state, and if not, why not (see
    /// <c>SettingStateResult.Outcome</c>). Drives the toggle's neutral overlay or the selection's
    /// adornment, which icon and short label they show, and whether clicking them does anything.</summary>
    public SettingDetectionOutcome Outcome { get; init; }
    public string OnText { get; init; } = "On";
    public string OffText { get; init; } = "Off";
    public string ActionButtonText { get; init; } = "Apply";

    /// <summary>Per-selection-option warning text, index-aligned with the options
    /// (null entries = no warning). Drives the option-warning status banner.</summary>
    public IReadOnlyList<string?>? OptionWarnings { get; init; }
}
