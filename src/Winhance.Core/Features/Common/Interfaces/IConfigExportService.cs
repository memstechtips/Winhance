using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigExportService
{
    Task ExportConfigurationAsync();
    Task CreateUserBackupConfigAsync();
    Task<Models.UnifiedConfigurationFile> CreateConfigurationFromSystemAsync(bool isBackup = false);

    /// <summary>
    /// Builds a configuration seeded from current system state, then overlays the edits
    /// recorded during the active Builder session (the user's authored intent). Used by
    /// Builder Save instead of <see cref="CreateConfigurationFromSystemAsync"/>.
    /// </summary>
    Task<Models.UnifiedConfigurationFile> CreateConfigurationFromUiStateAsync(bool isBackup = false);

    /// <summary>
    /// Builder Save (Config target): writes the UI-state configuration to a .winhance file
    /// via the save picker. Mirrors <see cref="ExportConfigurationAsync"/> but sources values
    /// from Builder edits rather than the live system.
    /// </summary>
    Task ExportBuilderConfigAsync();

    /// <summary>
    /// Builder Save (Autounattend target): builds the UI-state configuration and renders it
    /// to an autounattend.xml via the save picker. Uses the authored choices, not the system.
    /// </summary>
    Task ExportBuilderAutounattendAsync();
}
