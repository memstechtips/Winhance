namespace Winhance.UI.Features.Common.Interfaces;

public interface IWindowsVersionFilterService
{
    bool IsFilterEnabled { get; }

    void LoadFilterPreference();

    Task<bool> ToggleFilterAsync(bool isInReviewMode);

    // Not persisted.
    void ForceFilterOn();

    void RestoreFilterPreference();

    event EventHandler<bool>? FilterStateChanged;
}
