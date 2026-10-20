using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// One place that turns the UI filters into the scope the catalog registry answers over, so a caller never has
// to remember that a filter being ON means the scope flag is OFF. Builder gates on the Windows the file is for,
// which is why the title-bar version filter is ignored there.
public sealed class CatalogScopeProvider : ICatalogScopeProvider
{
    private readonly IWindowsVersionFilterService _versionFilter;
    private readonly IHardwareFilterService _hardwareFilter;
    private readonly ITargetWindowsService _targetWindows;
    private readonly IApplicationModeService _mode;

    public CatalogScopeProvider(
        IWindowsVersionFilterService versionFilter,
        IHardwareFilterService hardwareFilter,
        ITargetWindowsService targetWindows,
        IApplicationModeService mode)
    {
        _versionFilter = versionFilter;
        _hardwareFilter = hardwareFilter;
        _targetWindows = targetWindows;
        _mode = mode;
    }

    public CatalogScope Current => _mode.CurrentMode == WinhanceMode.Builder
        ? new(
            IncludeOtherOsVersions: _targetWindows.Build is null,
            IncludeOtherHardware: !_hardwareFilter.IsFilterEnabled,
            IncludeAnswerFileOnly: true,
            TargetBuild: _targetWindows.Build is { } build ? WindowsReleases.TargetOf(build) : null)
        : new(
            IncludeOtherOsVersions: !_versionFilter.IsFilterEnabled,
            IncludeOtherHardware: !_hardwareFilter.IsFilterEnabled);
}
