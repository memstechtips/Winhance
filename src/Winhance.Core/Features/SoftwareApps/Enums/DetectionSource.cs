namespace Winhance.Core.Features.SoftwareApps.Enums;

/// <summary>
/// Indicates which detection method discovered that an app is installed.
/// Used to determine the most appropriate uninstall method.
/// </summary>
public enum DetectionSource
{
    None,
    WinGet,
    Chocolatey,
    Registry
}
