namespace Winhance.Core.Features.Common.Enums;

public enum KeepRemovedMode
{
    // Winhance checks the kept list itself; no scripts on disk, no SYSTEM task.
    Winhance,

    // SYSTEM scheduled tasks run per-app .ps1 files from the locked ProgramData folder.
    Scripts,
}
