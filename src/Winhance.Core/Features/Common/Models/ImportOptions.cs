using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public record ImportOptions
    {
        public bool ProcessWindowsAppsRemoval { get; init; }
        public bool ProcessWindowsAppsInstallation { get; init; }
        public bool ProcessExternalAppsInstallation { get; init; }
        public bool ProcessExternalAppsRemoval { get; init; }
        public bool ApplyThemeWallpaper { get; init; }
        public bool ApplyCleanTaskbar { get; init; }
        public bool ApplyCleanStartMenu { get; init; }
        public bool ReviewBeforeApplying { get; init; }
        public IReadOnlyCollection<string> ActionOnlySubsections { get; init; } = new HashSet<string>();
    }
}
