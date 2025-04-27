namespace Winhance.Core.Features.Common.Enums;

public enum OptimizationCategory
{
    Privacy,
    Gaming,
    Updates,
    Performance,
    GamingandPerformance,
    Personalization,
    Taskbar,
    StartMenu,
    Explorer,
    Notifications,
    Sound,
    Accessibility,
    Search,
    Services,
    Power
}

public enum WindowsAppType
{
    AppX,
    Capability,
    Special  // For Edge, OneDrive, etc.
}

public enum ServiceStartupType
{
    Automatic = 2,
    Manual = 3,
    Disabled = 4
}