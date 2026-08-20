namespace Winhance.UI.Features.Common.Interfaces;

// true = hide settings this machine cannot have (the default, and forced outside Builder).
public interface IHardwareFilterService
{
    bool IsFilterEnabled { get; }

    Task SetAsync(bool enabled);

    Task ResetAsync();

    event EventHandler<bool>? FilterStateChanged;
}
