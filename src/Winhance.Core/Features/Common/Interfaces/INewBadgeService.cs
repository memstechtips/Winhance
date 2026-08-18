namespace Winhance.Core.Features.Common.Interfaces;

public interface INewBadgeService
{
    // Data-driven trigger: the highest AddedInVersion across the loaded registry must have increased since the last
    // run for an "effective upgrade" to register - decoupled from the csproj <Version> so dev builds behave like
    // release builds. Null, empty and unparseable entries are ignored.
    void Initialize(IEnumerable<string?> allAddedInVersions);

    bool IsSettingNew(string? addedInVersion, string settingId);

    // Auto-reset to true when Initialize detects an effective upgrade.
    bool ShowNewBadges { get; set; }
}
