namespace Winhance.UI.Features.Common.Interfaces;

public record NavBadgeUpdate(string Tag, int Count, string Style);

public interface INavBadgeService
{
    IReadOnlyList<NavBadgeUpdate> ComputeNavBadges();

    void SubscribeToSoftwareAppsChanges(Action onChanged);

    void UnsubscribeFromSoftwareAppsChanges();
}
