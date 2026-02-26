using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigExportService
{
    Task ExportConfigurationAsync();
    Task CreateUserBackupConfigAsync();
    Task<Models.UnifiedConfigurationFile> CreateConfigurationFromSystemAsync(bool isBackup = false);
}
