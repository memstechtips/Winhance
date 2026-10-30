using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record ItemDefinition : BaseDefinition
{
    public string[]? AppxPackageName { get; init; }
    public string[]? WinGetPackageId { get; init; }
    public string? MsStoreId { get; init; }
    public string? CapabilityName { get; init; }
    public string? OptionalFeatureName { get; init; }
    public string? ChocoPackageId { get; init; }
    // Replaces the winget manifest's InstallerSwitches entirely (winget install --override) - for upstream
    // manifests that pass broken switches to the installer.
    public string? WinGetInstallerOverride { get; init; }
    public bool CanBeReinstalled { get; init; } = true;
    public bool RequiresReboot { get; init; }
    // The taskbar keeps what the package put there until the shell restarts, so a removal raises the restart-Explorer bar.
    public bool RequiresExplorerRestart { get; init; }
    public Func<string>? RemovalScript { get; init; }
    // The script's name without ".ps1", also its startup task and log slot. Items that share a name share one file.
    public string? RemovalScriptName { get; init; }
    public string? RemovalScriptFileName => RemovalScriptName is null ? null : RemovalScriptName + ".ps1";
    // Its scheduled task fires at boot instead of at sign-in. Items that share a script must agree.
    public bool RemovalScriptRunsAtStartup { get; init; }
    // Ids of items this item's script removes as well, so selecting one of them beside this item needs no run of its own.
    public string[]? AlsoRemoves { get; init; }
    // Supports {version}, {arch}, {locale} placeholders.
    public string? RegistryDisplayName { get; init; }
    // Supports {version}, {arch}, {locale}; matched against SubKeyNames including SystemComponent=1 entries.
    public string? RegistrySubKeyName { get; init; }
    // Supports environment variables (%USERPROFILE%).
    public string[]? DetectionPaths { get; init; }
    public string[]? ProcessesToStop { get; init; }
    public string? WebsiteUrl { get; init; }
    // e.g. Microsoft Edge: removing it may break Windows components. Renders an amber Warning pill with a generic
    // localized message, so the flag is reusable without per-item text.
    public bool HasInstabilityWarning { get; init; }
    // A warning only some PCs get: WindowsAppsService turns HasInstabilityWarning on where RegistryKey exists, and
    // the pill shows TooltipKey's text instead of the generic one.
    public MachineWarning? MachineWarning { get; init; }
    public ExternalAppMetadata? ExternalApp { get; init; }

    // Mutable runtime state — set by WindowsAppsViewModel/ExternalAppsViewModel
    // via the relevant service (status discovery, icon resolver), proxied
    // through AppItemViewModel for UI binding.
    public bool IsInstalled { get; set; }
    public DetectionSource DetectedVia { get; set; }

    public string? IconPath { get; set; }
}

public sealed record MachineWarning(string RegistryKey, string TooltipKey);
