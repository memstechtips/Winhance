using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigReviewModeService
{
    bool IsInReviewMode { get; }
    bool IsWindowsDefaults { get; }
    WinhanceConfigFile? ActiveConfig { get; }
    Task EnterReviewModeAsync(WinhanceConfigFile config, bool isWindowsDefaults = false);
    void ExitReviewMode();
    event EventHandler? ReviewModeChanged;
}
