using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public class ImportOptions
    {
        public bool ProcessWindowsAppsRemoval { get; set; }
        public bool ProcessWindowsAppsInstallation { get; set; }
        public bool ProcessExternalAppsInstallation { get; set; }
        public bool ProcessExternalAppsRemoval { get; set; }
        public bool ApplyThemeWallpaper { get; set; }
        public bool ApplyCleanTaskbar { get; set; }
        public bool ApplyCleanStartMenu { get; set; }
        public bool ReviewBeforeApplying { get; set; }
        public HashSet<string> ActionOnlySubsections { get; set; } = new HashSet<string>();
    }
}
