using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class SelectionSetBuilder(
    ISettingSnapshotSource snapshot,
    IAppSelectionSource apps,
    IApplicationModeService mode,
    ICatalogScopeProvider scopeProvider,
    ICatalogSettingsRegistry registry,
    IInstallOptionsService installOptions) : ISelectionSetBuilder
{
    public CatalogScope CurrentScope => scopeProvider.Current;

    // The scope provider admits answer-file-only settings in Builder alone, and this path runs outside it.
    public async Task<SelectionSet> FromMachineAsync() =>
        new(await snapshot.CaptureAsync(CurrentScope with { IncludeAnswerFileOnly = true }), await apps.CheckedWindowsAppsAsync(), await apps.CheckedExternalAppsAsync())
        {
            InstallOptions = installOptions.Current,
        };

    public async Task<SelectionSet> FromMachineForBackupAsync() =>
        new(await snapshot.CaptureAsync(CurrentScope), await apps.InstalledWindowsAppsAsync(), Array.Empty<AppChoice>());

    // An edit for a setting the snapshot could not read (a power plan with no active scheme, a slider with no
    // value) is still the user's intent, so it is appended rather than dropped; the writers decide what the file can
    // hold by the catalog scope, so an edit outside it falls out there.
    public async Task<SelectionSet> FromBuilderSessionAsync()
    {
        var edits = mode.GetBuilderEdits().ToDictionary(e => e.SettingId, e => e.Value);
        var settings = (await snapshot.CaptureAsync(CurrentScope))
            .Select(c => edits.Remove(c.SettingId, out var authored) ? c with { Value = authored } : c)
            .ToList();
        settings.AddRange(edits.Select(e => new SettingChoice(e.Key, e.Value)));
        // Exclusion is absence. Applied after the append so an appended edit obeys it too, and here rather than in
        // ConfigFileMapper so both writers see one list and ConfigFileWriter's logged count still describes the file.
        settings.RemoveAll(c => !mode.IsIncluded(c.SettingId));

        // An Action - Clean Start Menu, Clean Taskbar - is the one kind with no value on the machine to capture
        // and none to leave out, so its include tick IS the choice: ticked means the file runs it. Whatever the
        // snapshot or a button press said about one is dropped first, so inclusion is the only thing deciding.
        var actions = registry.GetAll(CurrentScope).Values
            .SelectMany(byFeature => byFeature)
            .Where(setting => setting.Control == ControlKind.Action)
            .ToList();
        settings.RemoveAll(c => actions.Any(a => a.Id == c.SettingId));
        settings.AddRange(actions
            .Where(setting => mode.IsIncluded(setting.Id))
            .Select(setting => new SettingChoice(setting.Id, new ChoiceValue.Toggle(true))));

        var set = new SelectionSet(settings, await apps.CheckedWindowsAppsAsync(), await apps.CheckedExternalAppsAsync())
        {
            InstallOptions = installOptions.Current,
        };
        return mode.CurrentBuilderTarget == BuilderTarget.Autounattend ? WinhanceOnNewPc.Enforce(set) : set;
    }
}
