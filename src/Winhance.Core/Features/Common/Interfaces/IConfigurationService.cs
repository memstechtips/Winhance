namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigurationService
{
    Task ExportConfigurationAsync();
    Task ImportConfigurationAsync();
    Task ImportConfigurationFromFileAsync(string filePath);
    Task CreateUserBackupConfigAsync();
    Task ApplyReviewedConfigAsync();
    Task CancelReviewModeAsync();
}
