using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// One place that turns the UI filters into the scope the catalog registry answers over, so a caller never has
// to remember that a filter being ON means the scope flag is OFF. Builder gates on the Windows the file is for,
// which is why the title-bar version filter is ignored there.
public sealed class CatalogScopeProvider(
    IWindowsVersionFilterService versionFilter,
    IHardwareFilterService hardwareFilter,
    ITargetWindowsService targetWindows,
    IApplicationModeService mode) : ICatalogScopeProvider
{
    public CatalogScope Current => mode.CurrentMode == WinhanceMode.Builder
        ? new(
            IncludeOtherOsVersions: targetWindows.Build is null,
            IncludeOtherHardware: !hardwareFilter.IsFilterEnabled,
            IncludeAnswerFileOnly: true,
            TargetBuild: targetWindows.Build is { } build ? WindowsReleases.TargetOf(build) : null)
        : new(
            IncludeOtherOsVersions: !versionFilter.IsFilterEnabled,
            IncludeOtherHardware: !hardwareFilter.IsFilterEnabled);
}
