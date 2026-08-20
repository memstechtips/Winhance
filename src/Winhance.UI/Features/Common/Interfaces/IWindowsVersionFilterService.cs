namespace Winhance.UI.Features.Common.Interfaces;

public interface IWindowsVersionFilterService
{
    bool IsFilterEnabled { get; }

    Task LoadFilterPreferenceAsync();

    Task<bool> ToggleFilterAsync(bool isInReviewMode);

    // Not persisted.
    void ForceFilterOn();

    Task RestoreFilterPreferenceAsync();

    event EventHandler<bool>? FilterStateChanged;
}
