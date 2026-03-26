namespace Winhance.Core.Features.Common.Interfaces;

public interface INewBadgeService
{
    void Initialize();
    bool IsSettingNew(string? addedInVersion, string settingId);
    void DismissBadge(string settingId);
}
