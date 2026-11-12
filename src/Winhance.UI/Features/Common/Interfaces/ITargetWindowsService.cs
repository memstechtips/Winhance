namespace Winhance.UI.Features.Common.Interfaces;

// The Windows build a Builder file is made for; null = every version.
public interface ITargetWindowsService
{
    int? Build { get; }

    void Set(int? build);

    void Reset();

    event EventHandler? Changed;
}
