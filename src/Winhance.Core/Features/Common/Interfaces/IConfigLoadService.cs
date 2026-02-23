using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IConfigLoadService
    {
        Task<UnifiedConfigurationFile?> LoadAndValidateConfigurationFromFileAsync();
        Task<UnifiedConfigurationFile?> LoadRecommendedConfigurationAsync();
        Task<UnifiedConfigurationFile?> LoadWindowsDefaultsConfigurationAsync();
        Task<UnifiedConfigurationFile?> LoadUserBackupConfigurationAsync();
        List<string> DetectIncompatibleSettings(UnifiedConfigurationFile config);
        UnifiedConfigurationFile FilterConfigForCurrentSystem(UnifiedConfigurationFile config);
    }
}
