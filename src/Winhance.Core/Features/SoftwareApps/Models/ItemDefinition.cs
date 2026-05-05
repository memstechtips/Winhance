using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record ItemDefinition : BaseDefinition
{
    // Immutable definition properties
    public new InputType InputType { get; init; } = InputType.CheckBox;
    public string[]? AppxPackageName { get; init; }
    public string[]? WinGetPackageId { get; init; }
    public string? MsStoreId { get; init; }
    public string? CapabilityName { get; init; }
    public string? OptionalFeatureName { get; init; }
    public string? ChocoPackageId { get; init; }
    /// <summary>
    /// When set, replaces the winget manifest's InstallerSwitches entirely via `winget install --override "<value>"`.
    /// Use to work around upstream manifests that pass broken switches to the underlying installer.
    /// </summary>
    public string? WinGetInstallerOverride { get; init; }
    public bool CanBeReinstalled { get; init; } = true;
    public bool RequiresReboot { get; init; }
    public Func<string>? RemovalScript { get; init; }
    /// <summary>
    /// Pattern for registry DisplayName matching. Supports {version}, {arch}, {locale} placeholders.
    /// When set, compared against registry DisplayNames.
    /// </summary>
    public string? RegistryDisplayName { get; init; }
    /// <summary>
    /// Pattern for registry SubKeyName matching. Supports {version}, {arch}, {locale} placeholders.
    /// When set, compared against registry SubKeyNames (including SystemComponent=1 entries).
    /// </summary>
    public string? RegistrySubKeyName { get; init; }
    public string? RegistryUninstallSearchPattern { get; init; }
    /// <summary>
    /// Paths to check for existence (file or directory) as a detection fallback.
    /// Supports environment variables (e.g. %USERPROFILE%).
    /// </summary>
    public string[]? DetectionPaths { get; init; }
    public string[]? ProcessesToStop { get; init; }
    public string? WebsiteUrl { get; init; }
    /// <summary>
    /// Optional warning text shown beneath the description in Card view when the
    /// item carries a meaningful uninstall risk (e.g. Microsoft Edge: "Uninstalling
    /// Edge may cause system instability"). Null/empty → no warning row rendered.
    /// </summary>
    public string? UninstallWarning { get; init; }
    public ExternalAppMetadata? ExternalApp { get; init; }

    /// <summary>
    /// Ordered list of icon sources to try when local extraction (AppX / Binary)
    /// and the Microsoft Store CDN both come up empty. Each entry is one of:
    /// <list type="bullet">
    /// <item><description>An <c>http(s)://</c> URL — fetched at runtime and cached locally.
    /// Vendor-canonical URLs only (vendor's site / CDN, the project's GitHub repo,
    /// Wikimedia Commons rasterized PNGs). No third-party image hosts —
    /// Winhance fetches at runtime, so URL stability matters.</description></item>
    /// <item><description>A <c>data:image/&lt;type&gt;;base64,&lt;payload&gt;</c> URI —
    /// the base64 payload is decoded directly into the cache. Useful when a vendor
    /// only ships their logo embedded in HTML/CSS and there's no stable raw URL.</description></item>
    /// <item><description>A local file path — checked with <c>File.Exists</c> after
    /// <c>Environment.ExpandEnvironmentVariables</c>. Icon files (<c>.ico</c>,
    /// <c>.png</c>, ...) are read directly. Win32 executables (<c>.exe</c>,
    /// <c>.dll</c>) are routed through the binary icon extractor — the same code
    /// path Layer 1b uses — which lets entries reuse system binaries (e.g.
    /// <c>%SystemRoot%\explorer.exe</c> for ExplorerPatcher) without per-app
    /// special-casing. Useful when an app leaves a usable icon file on disk after
    /// uninstall (e.g. OneDrive's <c>%SystemRoot%\System32\OneDrive.ico</c> stays
    /// around even when the OneDrive client is removed).</description></item>
    /// </list>
    /// Sources are tried in array order; first one that yields a non-empty image
    /// wins. List local paths first when you have them — they're zero-network and
    /// can't rot.
    ///
    /// When this is set, it's treated as the canonical visual identity for the
    /// entry: the resolver runs IconSources before AppX (Layer 1a), Binary
    /// extraction (Layer 1b), and Store CDN (Layer 2a). Those layers only run
    /// as fallback when IconSources is null/empty or every entry in the array
    /// failed to fetch.
    /// </summary>
    public string[]? IconSources { get; init; }

    // Mutable runtime state — set by WindowsAppsViewModel/ExternalAppsViewModel
    // via the relevant service (status discovery, icon resolver), proxied
    // through AppItemViewModel for UI binding.
    public bool IsInstalled { get; set; }
    public DetectionSource DetectedVia { get; set; }

    /// <summary>
    /// Absolute path to the cached icon PNG, or null if no icon is available.
    /// Populated by IAppIconResolver from WindowsAppsViewModel after install-status
    /// discovery; null for capabilities, optional features, and not-installed AppX entries.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// Absolute path to the installed binary (.exe, .ico, or install folder) used
    /// by Layer 1b of the icon resolver to extract a Win32 icon. Stamped by
    /// AppStatusDiscoveryService from registry DisplayIcon / InstallLocation
    /// during the uninstall-key walk, or from a matched DetectionPaths entry.
    /// Null for AppX entries (Layer 1a handles them) and for entries with no
    /// install hint (Layer 2 takes over).
    /// </summary>
    public string? InstalledBinaryHint { get; set; }
}