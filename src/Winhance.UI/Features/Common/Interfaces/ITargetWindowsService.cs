namespace Winhance.UI.Features.Common.Interfaces;

// The Windows build a Builder file is made for; null = every version.
public interface ITargetWindowsService
{
    int? Build { get; }

    Task SetAsync(int? build);

    Task ResetAsync();

    event EventHandler? Changed;
}
