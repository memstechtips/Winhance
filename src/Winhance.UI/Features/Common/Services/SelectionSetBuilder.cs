using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class SelectionSetBuilder : ISelectionSetBuilder
{
    private readonly ISettingSnapshotSource _snapshot;
    private readonly IAppSelectionSource _apps;
    private readonly IApplicationModeService _mode;
    private readonly IWindowsVersionFilterService _versionFilter;

    public SelectionSetBuilder(ISettingSnapshotSource snapshot, IAppSelectionSource apps, IApplicationModeService mode, IWindowsVersionFilterService versionFilter)
    {
        _snapshot = snapshot;
        _apps = apps;
        _mode = mode;
        _versionFilter = versionFilter;
    }

    public CatalogScope CurrentScope => new(IncludeOtherOsVersions: !_versionFilter.IsFilterEnabled, IncludeOtherHardware: false);

    public async Task<SelectionSet> FromMachineAsync() =>
        new(await _snapshot.CaptureAsync(CurrentScope), await _apps.CheckedWindowsAppsAsync(), await _apps.CheckedExternalAppsAsync(), AutounattendChoices.None);

    public async Task<SelectionSet> FromMachineForBackupAsync() =>
        new(await _snapshot.CaptureAsync(CurrentScope), await _apps.InstalledWindowsAppsAsync(), Array.Empty<AppChoice>(), AutounattendChoices.None);

    // An edit for a setting the snapshot does not contain is dropped on purpose: the machine's catalog scope decides
    // what the file can hold, and the hardware filter (Phase 6) is how a user widens that scope.
    public async Task<SelectionSet> FromBuilderSessionAsync()
    {
        var edits = _mode.GetBuilderEdits().ToDictionary(e => e.SettingId, e => e.Value);
        var settings = (await _snapshot.CaptureAsync(CurrentScope))
            .Select(c => edits.TryGetValue(c.SettingId, out var authored) ? c with { Value = authored } : c)
            .ToList();
        return new SelectionSet(settings, await _apps.CheckedWindowsAppsAsync(), await _apps.CheckedExternalAppsAsync(), AutounattendChoices.None);
    }
}
