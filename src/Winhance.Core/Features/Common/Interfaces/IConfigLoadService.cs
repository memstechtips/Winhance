using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigLoadService
{
    Task<WinhanceConfigFile?> LoadAndValidateConfigurationFromFileAsync();
    Task<WinhanceConfigFile?> LoadRecommendedConfigurationAsync();
    Task<WinhanceConfigFile?> LoadWindowsDefaultsConfigurationAsync();
    Task<WinhanceConfigFile?> LoadUserBackupConfigurationAsync();
    List<string> DetectIncompatibleSettings(WinhanceConfigFile config);
    WinhanceConfigFile FilterConfigForCurrentSystem(WinhanceConfigFile config);
}
