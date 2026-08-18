using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Localization;

/// <summary>
/// Pure, dependency-free builder for the localization-key strings a
/// setting resolves at runtime. The key formats here MUST stay
/// byte-identical to the inline construction in
/// <c>Winhance.UI.Features.Common.Services.SettingLocalizationService</c> — that service
/// delegates to this class, and the integration tests reuse it to assert that every key a
/// setting requests actually exists in <c>en.json</c>.
///
/// All key bases use <c>LocalizationId ?? Id</c> (NOT <c>Id</c> alone) so OS-gated setting
/// variants can share one set of localization entries.
/// </summary>
public static class SettingLocalizationKeys
{
    /// <summary>
    /// The localized name for a state detection could not place. One string for every setting - the
    /// per-setting override key that used to sit alongside this was removed on 2026-07-27 with the
    /// synthetic "Custom" dropdown entry it named.
    /// </summary>
    public const string CommonCustomState = "Common_CustomState";

    // The catalog Id IS the canonical, alias-normalized id, so it equals LocalizationId ?? Id for every setting.
    private static string Base(Setting setting) => setting.Id;

    public static string Name(Setting setting) => $"Setting_{Base(setting)}_Name";
    public static string Description(Setting setting) => $"Setting_{Base(setting)}_Description";
    public static string OptionDisplay(Setting setting, int index) => $"Setting_{Base(setting)}_Option_{index}";
    public static string OptionTooltip(Setting setting, int index) => $"Setting_{Base(setting)}_OptionTooltip_{index}";
    public static string OptionWarning(Setting setting, int index) => $"Setting_{Base(setting)}_OptionWarning_{index}";


    /// <summary>
    /// Compacted group key, e.g. group name "Privacy &amp; Security" -&gt; <c>SettingGroup_PrivacySecurity</c>.
    /// Spaces and ampersands are removed.
    /// </summary>
    public static string GroupCompact(string groupName) =>
        $"SettingGroup_{groupName.Replace(" ", "").Replace("&", "")}";

    /// <summary>
    /// Snake-case group key, e.g. "Content Delivery &amp; Advertising" -&gt;
    /// <c>SettingGroup_Content_Delivery_Advertising</c>. " &amp; " and " " become "_", "&amp;" becomes "_",
    /// and runs of "__" collapse to a single "_".
    /// </summary>
    public static string GroupSnake(string groupName)
    {
        var snakeCaseName = groupName
            .Replace(" & ", "_")
            .Replace(" ", "_")
            .Replace("&", "_");

        while (snakeCaseName.Contains("__"))
        {
            snakeCaseName = snakeCaseName.Replace("__", "_");
        }

        return $"SettingGroup_{snakeCaseName}";
    }

    /// <summary>
    /// True when <paramref name="value"/> is already a localization key (and so should be used
    /// verbatim as a lookup key rather than having a per-setting option key constructed for it).
    /// </summary>
    public static bool IsLocalizationKey(string value)
    {
        return value.StartsWith("Template_") ||
               value.StartsWith("Setting_") ||
               value.StartsWith("PowerPlan_") ||
               value.StartsWith("ServiceOption_");
    }

    /// <summary>
    /// The COMPLETE set of localization keys this setting requests at runtime.
    /// Name/Description always; group keys (compact + snake) when <c>Display.GroupName != null</c>;
    /// the option block (per-setting Custom override + <c>Common_CustomState</c> + per-state option-display/tooltip/
    /// warning) for a Selection setting (a Toggle, Slider, Action, or dynamic PowerPlan carries no enumerated
    /// option keys). Per state: OptionDisplay only when the label is NOT itself a localization key;
    /// OptionTooltip/OptionWarning only when the state carries a non-empty tooltip/warning.
    /// </summary>
    public static IEnumerable<string> ExpectedKeys(Setting setting)
    {
        yield return Name(setting);
        yield return Description(setting);

        if (setting.Display.GroupName != null)
        {
            yield return GroupCompact(setting.Display.GroupName);
            yield return GroupSnake(setting.Display.GroupName);
        }

        if (setting.Control == ControlKind.Selection)
        {
            // No per-setting Custom-option key any more: the synthetic "Custom" dropdown entry it named
            // is gone, and its friendlier "Custom (User Defined)" wording was dropped on 2026-07-27 for
            // asserting a cause we cannot know. Every setting now uses the one CommonCustomState string.
            yield return CommonCustomState;

            for (int i = 0; i < setting.States.Count; i++)
            {
                var state = setting.States[i];

                if (!IsLocalizationKey(state.Label))
                {
                    yield return OptionDisplay(setting, i);
                }

                if (!string.IsNullOrEmpty(state.Tooltip))
                {
                    yield return OptionTooltip(setting, i);
                }

                if (!string.IsNullOrEmpty(state.Warning))
                {
                    yield return OptionWarning(setting, i);
                }
            }
        }
    }
}
