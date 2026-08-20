using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigReviewModeService
{
    bool IsInReviewMode { get; }
    bool IsWindowsDefaults { get; }
    UnifiedConfigurationFile? ActiveConfig { get; }
    Task EnterReviewModeAsync(UnifiedConfigurationFile config, bool isWindowsDefaults = false);
    void ExitReviewMode();
    event EventHandler? ReviewModeChanged;
}
