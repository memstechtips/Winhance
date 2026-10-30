using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigLoadService
{
    Task<WinhanceConfigFile?> LoadAndValidateConfigurationFromFileAsync();
    Task<WinhanceConfigFile?> LoadAndValidateConfigurationAsync(string filePath);
    Task<WinhanceConfigFile?> LoadRecommendedConfigurationAsync();
    Task<WinhanceConfigFile?> LoadWindowsDefaultsConfigurationAsync();
    Task<WinhanceConfigFile?> LoadUserBackupConfigurationAsync();

    // No dialogs, for the profile switch from the tray and the sign-in task: null and a log line when it cannot load.
    Task<WinhanceConfigFile?> ReadConfigurationAsync(string filePath);

    // The same check over bytes the caller already read, so the bytes it hashed are the bytes it applies.
    WinhanceConfigFile? ParseConfiguration(byte[] content, string source);
    Task<WinhanceConfigFile?> ReadEmbeddedConfigurationAsync(bool windowsDefaults);
    List<string> DetectIncompatibleSettings(WinhanceConfigFile config);
    WinhanceConfigFile FilterConfigForCurrentSystem(WinhanceConfigFile config);
}
