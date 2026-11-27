namespace Winhance.UI.Features.Common.Interfaces;

// true = hide settings this machine cannot have (the default, and forced outside Builder).
public interface IHardwareFilterService
{
    bool IsFilterEnabled { get; }

    void Set(bool enabled);

    void Reset();

    event EventHandler<bool>? FilterStateChanged;
}
