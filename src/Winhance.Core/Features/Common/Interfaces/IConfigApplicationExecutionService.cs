using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigApplicationExecutionService
{
    Task ExecuteConfigImportAsync(WinhanceConfigFile config, ImportOptions options);
    Task ApplyConfigurationWithOptionsAsync(
        WinhanceConfigFile config,
        List<string> selectedSections,
        ImportOptions options);

    // No overlay, no dialogs: the profile switch from the tray and the sign-in task. Returns the settings not applied.
    // Per-setting restarts are held back while it runs, so the caller says which of the two closing steps are due:
    // the one Explorer restart and the regional-settings broadcast.
    Task<IReadOnlyList<string>> ApplySettingsQuietlyAsync(
        WinhanceConfigFile config, string sourceName, bool isWindowsDefaults, bool restartExplorer, bool broadcastRegional);
}
