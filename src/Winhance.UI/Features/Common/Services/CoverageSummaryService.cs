using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

public sealed class CoverageSummaryService(
    IConfigImportState importState,
    IConfigLoadService configLoad,
    ICatalogSettingsRegistry registry,
    IFileSystemService files,
    IDialogService dialogs,
    ILocalizationService loc,
    ILogService log,
    IUserPreferencesService preferences,
    ISettingSnapshotSource snapshot,
    IVersionService version) : ICoverageSummaryService
{
    public async Task ShowImportSummaryAsync(IReadOnlyList<string> setAside, IReadOnlyList<string> notApplied)
    {
        var path = importState.SourcePath;
        var coverage = path is null ? ConfigCoverage.Empty : await MeasureAsync(path);

        var sections = new List<SummarySection>();
        if (notApplied.Count > 0)
            sections.Add(new(loc.GetString("Config_Import_NotApplied_Heading"), notApplied.Select(line => new SummaryRow(SummaryIcon.Status, line, Tone: SummaryTone.Error)).ToList()));
        if (setAside.Count > 0)
            sections.Add(new(loc.GetString("Config_SetAside_Format", setAside.Count), setAside.Select(name => new SummaryRow(SummaryIcon.Status, name)).ToList()));
        if (coverage.Retired.Count > 0)
            sections.Add(new(loc.GetString("Config_Import_Retired_Heading", coverage.Retired.Count), coverage.Retired.Select(RetiredRow).ToList()));
        if (coverage.Missing.Count > 0)
            sections.Add(MissingSection(coverage.Missing));

        var asked = await dialogs.AskSummaryAsync(new SummaryRequest
        {
            Title = loc.GetString("Config_Import_Success_Title"),
            Message = loc.GetString("Config_Import_Success_Message"),
            Verdict = coverage.IsComplete ? null : new SummaryVerdict(SummaryTone.Warning, loc.GetString("Config_Import_OutOfDate_Title"), OutOfDateMessage(coverage)),
            Sections = sections,
            CopyText = coverage.Missing.Count > 0 ? CopyText(coverage.Missing) : null,
            ConfirmButtonText = coverage.CanUpdate ? loc.GetString("Config_Import_SaveUpdatedCopy") : string.Empty,
        });

        if (asked.Confirmed && path is not null)
            await SaveUpdatedCopyAsync(path);
    }

    public async Task<bool> ShowProfileReminderAsync(SettingsProfile profile, WinhanceConfigFile config)
    {
        var missing = await ProfileGapsAsync(profile, config);
        if (missing.Count == 0)
            return false;

        var asked = await dialogs.AskSummaryAsync(new SummaryRequest
        {
            Title = loc.GetString("Profiles_Reminder_Title"),
            Verdict = new SummaryVerdict(SummaryTone.Warning, loc.GetString("Profiles_Reminder_Verdict", profile.Name), loc.GetString("Profiles_Reminder_Message")),
            Sections = [MissingSection(missing)],
            CopyText = CopyText(missing),
            CheckboxText = loc.GetString("Profiles_Reminder_DontShow"),
            ConfirmButtonText = loc.GetString("Profiles_EditInBuilder"),
            CancelButtonText = loc.GetString("Button_Close"),
        });
        if (asked.CheckboxChecked)
            await TurnOffReminderAsync(profile);
        return asked.Confirmed;
    }

    public async Task<int> TakeProfileReminderAsync(SettingsProfile profile, WinhanceConfigFile config)
    {
        var missing = await ProfileGapsAsync(profile, config);
        if (missing.Count > 0)
            await TurnOffReminderAsync(profile);
        return missing.Count;
    }

    // A built-in profile ships with this version, so whatever it leaves out it leaves out on purpose.
    private async Task<IReadOnlyList<Setting>> ProfileGapsAsync(SettingsProfile profile, WinhanceConfigFile config)
    {
        if (profile.Kind != ProfileKind.User || preferences.GetPreference(ReminderKey(profile), string.Empty) == CurrentVersion)
            return [];
        try
        {
            return ConfigCoverage.Measure(config, await SavableAsync()).Missing;
        }
        catch (Exception ex)
        {
            log.Log(LogLevel.Warning, $"Could not measure profile '{profile.Name}' against this version: {ex.Message}");
            return [];
        }
    }

    private Task TurnOffReminderAsync(SettingsProfile profile) => preferences.SetPreferenceAsync(ReminderKey(profile), CurrentVersion);

    private static string ReminderKey(SettingsProfile profile) => UserPreferenceKeys.ProfileReminderOffPrefix + profile.Id;

    private string CurrentVersion => version.GetCurrentVersion().Version;

    private async Task<ConfigCoverage> MeasureAsync(string path)
    {
        try
        {
            return ConfigCoverage.Measure(await ReadUnmigratedAsync(path), await SavableAsync());
        }
        catch (Exception ex)
        {
            // The import already happened; a file that cannot be re-read only costs the coverage report.
            log.Log(LogLevel.Warning, $"Could not measure {path} against this version: {ex.Message}");
            return ConfigCoverage.Empty;
        }
    }

    // Only what an export from this PC would hold: a setting it cannot read (a wallpaper colour while a picture is
    // set) is left out of every file it saves, so its absence says nothing about the file's age.
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<Setting>>> SavableAsync()
    {
        await registry.InitializeAsync();
        var readable = (await snapshot.CaptureAsync(CatalogScope.CurrentMachine)).Select(c => c.SettingId).ToHashSet(StringComparer.Ordinal);
        return registry.GetAll(CatalogScope.CurrentMachine)
            .ToDictionary(feature => feature.Key, feature => (IReadOnlyList<Setting>)feature.Value.Where(s => readable.Contains(s.Id)).ToList());
    }

    private async Task SaveUpdatedCopyAsync(string path)
    {
        try
        {
            var config = await configLoad.ReadConfigurationAsync(path) ?? throw new InvalidDataException(path);
            var updated = ConfigCoverage.Updated(config);
            var target = CopyPathFor(path);
            await files.WriteAllTextAsync(target, JsonSerializer.Serialize(updated, ConfigFileConstants.JsonOptions));
            log.Log(LogLevel.Info, $"Updated copy of {path} saved to {target}");
            await dialogs.ShowInformationAsync(loc.GetString("Config_Import_UpdatedCopySaved", target), loc.GetString("Config_Import_SaveUpdatedCopy"));
        }
        catch (Exception ex)
        {
            log.Log(LogLevel.Error, $"Could not save an updated copy of {path}: {ex.Message}");
            dialogs.ShowMessage(loc.GetString("Config_Import_UpdatedCopyFailed", ex.Message), loc.GetString("Dialog_Error"));
        }
    }

    // Coverage is measured on the file as written; the shared reader migrates, which would hide the old ids and features.
    private async Task<WinhanceConfigFile> ReadUnmigratedAsync(string path) =>
        JsonSerializer.Deserialize<WinhanceConfigFile>(await files.ReadAllTextAsync(path), ConfigFileConstants.JsonOptions)
            ?? throw new InvalidDataException(path);

    // Next to the original, never over it: the user keeps the file they made.
    private string CopyPathFor(string path)
    {
        var folder = files.GetDirectoryName(path) ?? string.Empty;
        var name = files.GetFileNameWithoutExtension(path);
        var suffix = loc.GetString("Config_Import_UpdatedCopySuffix");
        var candidate = files.CombinePath(folder, $"{name} ({suffix}){ConfigFileConstants.FileExtension}");
        for (var n = 2; files.FileExists(candidate); n++)
            candidate = files.CombinePath(folder, $"{name} ({suffix} {n}){ConfigFileConstants.FileExtension}");
        return candidate;
    }

    private string OutOfDateMessage(ConfigCoverage coverage)
    {
        var lines = new List<string>();
        if (coverage.Missing.Count > 0)
            lines.Add(loc.GetString("Config_Import_Missing_Message"));
        if (coverage.CanUpdate)
            lines.Add(loc.GetString("Config_Import_UpdateCopy_Message"));
        return string.Join("\n\n", lines);
    }

    private SummarySection MissingSection(IReadOnlyList<Setting> missing) =>
        new(loc.GetString("Config_Import_Missing_Heading", missing.Count),
            missing.Select(setting => new SummaryRow(SummaryIcon.Status, NameOf(setting), FeatureOf(setting), Tone: SummaryTone.Warning)).ToList());

    // One per line, so the list pastes into Notepad as a checklist.
    private string CopyText(IReadOnlyList<Setting> missing) =>
        string.Join(Environment.NewLine, missing.Select(setting => $"{FeatureOf(setting)}: {NameOf(setting)}"));

    // A retired item's name key went with its setting, so the file's own name and id are all there is to show.
    private static SummaryRow RetiredRow(ConfigurationItem item) =>
        new(SummaryIcon.Status, string.IsNullOrEmpty(item.Name) ? item.Id : item.Name, Code: item.Id);

    private string NameOf(Setting setting) => loc.SettingName(setting);

    private string FeatureOf(Setting setting) => loc.FeatureName(setting.Id, string.Empty);
}
