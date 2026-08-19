namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigExportService
{
    Task ExportConfigurationAsync();
    Task CreateUserBackupConfigAsync();
    Task<Models.WinhanceConfigFile> CreateConfigurationFromSystemAsync(bool isBackup = false);

    // Seeded from current system state, then overlaid with the edits recorded in the active Builder session.
    Task<Models.WinhanceConfigFile> CreateConfigurationFromUiStateAsync(bool isBackup = false);

    Task ExportBuilderConfigAsync();

    Task ExportBuilderAutounattendAsync();
}
