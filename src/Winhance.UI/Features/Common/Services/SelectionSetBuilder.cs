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
    private readonly ICatalogScopeProvider _scopeProvider;
    private readonly ICatalogSettingsRegistry _registry;

    public SelectionSetBuilder(ISettingSnapshotSource snapshot, IAppSelectionSource apps, IApplicationModeService mode, ICatalogScopeProvider scopeProvider, ICatalogSettingsRegistry registry)
    {
        _snapshot = snapshot;
        _apps = apps;
        _mode = mode;
        _scopeProvider = scopeProvider;
        _registry = registry;
    }

    public CatalogScope CurrentScope => _scopeProvider.Current;

    // The scope provider admits answer-file-only settings in Builder alone, and this path runs outside it.
    public async Task<SelectionSet> FromMachineAsync() =>
        new(await _snapshot.CaptureAsync(CurrentScope with { IncludeAnswerFileOnly = true }), await _apps.CheckedWindowsAppsAsync(), await _apps.CheckedExternalAppsAsync());

    public async Task<SelectionSet> FromMachineForBackupAsync() =>
        new(await _snapshot.CaptureAsync(CurrentScope), await _apps.InstalledWindowsAppsAsync(), Array.Empty<AppChoice>());

    // An edit for a setting the snapshot could not read (a power plan with no active scheme, a slider with no
    // value) is still the user's intent, so it is appended rather than dropped; the writers decide what the file can
    // hold by the catalog scope, so an edit outside it falls out there.
    public async Task<SelectionSet> FromBuilderSessionAsync()
    {
        var edits = _mode.GetBuilderEdits().ToDictionary(e => e.SettingId, e => e.Value);
        var settings = (await _snapshot.CaptureAsync(CurrentScope))
            .Select(c => edits.Remove(c.SettingId, out var authored) ? c with { Value = authored } : c)
            .ToList();
        settings.AddRange(edits.Select(e => new SettingChoice(e.Key, e.Value)));
        // Exclusion is absence. Applied after the append so an appended edit obeys it too, and here rather than in
        // ConfigFileMapper so both writers see one list and ConfigFileWriter's logged count still describes the file.
        settings.RemoveAll(c => !_mode.IsIncluded(c.SettingId));

        // An Action - Clean Start Menu, Clean Taskbar - is the one kind with no value on the machine to capture
        // and none to leave out, so its include tick IS the choice: ticked means the file runs it. Whatever the
        // snapshot or a button press said about one is dropped first, so inclusion is the only thing deciding.
        var actions = _registry.GetAll(CurrentScope).Values
            .SelectMany(byFeature => byFeature)
            .Where(setting => setting.Control == ControlKind.Action)
            .ToList();
        settings.RemoveAll(c => actions.Any(a => a.Id == c.SettingId));
        settings.AddRange(actions
            .Where(setting => _mode.IsIncluded(setting.Id))
            .Select(setting => new SettingChoice(setting.Id, new ChoiceValue.Toggle(true))));

        return new SelectionSet(settings, await _apps.CheckedWindowsAppsAsync(), await _apps.CheckedExternalAppsAsync());
    }
}
