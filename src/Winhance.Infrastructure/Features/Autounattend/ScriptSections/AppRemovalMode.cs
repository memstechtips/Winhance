namespace Winhance.Infrastructure.Features.Autounattend.ScriptSections;

// What the new PC does about removed apps coming back, chosen by the answer file's Removed apps setting.
internal enum AppRemovalMode
{
    // Remove once; if an update is staged, once more after it installs.
    Once,

    // Per-app scripts and SYSTEM scheduled tasks keep them removed.
    Scripts,

    // Winhance keeps them removed from the kept list the file records.
    Winhance,
}
