using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigReviewModeService
{
    bool IsInReviewMode { get; }
    bool IsWindowsDefaults { get; }
    WinhanceConfigFile? ActiveConfig { get; }

    // Settings the file carried that this PC cannot show, as "Name (Feature)". Empty outside a review.
    IReadOnlyList<string> SetAside { get; }

    Task EnterReviewModeAsync(WinhanceConfigFile config, bool isWindowsDefaults = false, IReadOnlyList<string>? setAside = null);
    void ExitReviewMode();
    event EventHandler? ReviewModeChanged;
}
