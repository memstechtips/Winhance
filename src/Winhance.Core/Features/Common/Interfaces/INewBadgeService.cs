namespace Winhance.Core.Features.Common.Interfaces;

public interface INewBadgeService
{
    void Initialize();
    bool IsSettingNew(string? addedInVersion, string settingId);

    /// <summary>
    /// Whether NEW badges should be shown globally. Bound to the View → NEW Badges
    /// toggle. Auto-reset to true when an app-version upgrade is detected during
    /// Initialize().
    /// </summary>
    bool ShowNewBadges { get; set; }
}
