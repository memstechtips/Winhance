using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigReviewOrchestrationService
{
    Task EnterReviewModeAsync(WinhanceConfigFile config, bool isWindowsDefaults, string? sourcePath);
    Task ApplyReviewedConfigAsync();
    Task CancelReviewModeAsync();
}
