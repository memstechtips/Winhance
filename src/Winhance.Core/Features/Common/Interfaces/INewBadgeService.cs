namespace Winhance.Core.Features.Common.Interfaces;

public interface INewBadgeService
{
    /// <summary>
    /// Initializes the badge baseline. Trigger is data-driven: the highest
    /// <c>AddedInVersion</c> across the loaded settings registry must have
    /// increased since the last run for an "effective upgrade" to register.
    /// This decouples badge behaviour from the csproj &lt;Version&gt; so dev
    /// builds behave identically to release builds.
    /// </summary>
    /// <param name="allAddedInVersions">
    /// Every <c>AddedInVersion</c> string in the loaded registry. Null / empty
    /// entries are ignored. Unparseable entries are ignored.
    /// </param>
    void Initialize(IEnumerable<string?> allAddedInVersions);

    bool IsSettingNew(string? addedInVersion, string settingId);

    /// <summary>
    /// Whether NEW badges should be shown globally. Bound to the View → NEW Badges
    /// toggle. Auto-reset to true when an effective upgrade is detected during
    /// Initialize().
    /// </summary>
    bool ShowNewBadges { get; set; }
}
