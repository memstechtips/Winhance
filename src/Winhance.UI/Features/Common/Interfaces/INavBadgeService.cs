namespace Winhance.UI.Features.Common.Interfaces;

public record NavBadgeUpdate(string Tag, int Count, string Style);

public interface INavBadgeService
{
    IReadOnlyList<NavBadgeUpdate> ComputeNavBadges();

    int GetSoftwareAppsSelectedCount();

    void SubscribeToSoftwareAppsChanges(Action onChanged);

    void UnsubscribeFromSoftwareAppsChanges();

    bool IsSoftwareAppsBadgeSubscribed { get; }
}
