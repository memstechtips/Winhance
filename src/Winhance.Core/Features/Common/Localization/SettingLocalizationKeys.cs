using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Localization;

// Key formats MUST stay byte-identical to SettingLocalizationService, which delegates here; the integration
// tests reuse this to assert every key a setting requests exists in en.json. Key bases use LocalizationId ?? Id
// so OS-gated setting variants share one set of entries.
public static class SettingLocalizationKeys
{
    public const string CommonCustomState = "Common_CustomState";

    // The catalog Id IS the canonical, alias-normalized id, so it equals LocalizationId ?? Id for every setting.
    private static string Base(Setting setting) => setting.Id;

    public static string Name(Setting setting) => $"Setting_{Base(setting)}_Name";
    public static string Description(Setting setting) => $"Setting_{Base(setting)}_Description";
    public static string OptionDisplay(Setting setting, int index) => $"Setting_{Base(setting)}_Option_{index}";
    public static string OptionTooltip(Setting setting, int index) => $"Setting_{Base(setting)}_OptionTooltip_{index}";
    public static string OptionWarning(Setting setting, int index) => $"Setting_{Base(setting)}_OptionWarning_{index}";


    public static string GroupCompact(string groupName) =>
        $"SettingGroup_{groupName.Replace(" ", "").Replace("&", "")}";

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

    public static bool IsLocalizationKey(string value)
    {
        return value.StartsWith("Template_") ||
               value.StartsWith("Setting_") ||
               value.StartsWith("PowerPlan_") ||
               value.StartsWith("ServiceOption_");
    }

    // The COMPLETE set a setting requests at runtime: Name/Description always; group keys when GroupName != null;
    // option keys only for a Selection (a Toggle, Slider, Action or dynamic PowerPlan carries none). Per state:
    // OptionDisplay only when the label is not itself a key; tooltip/warning only when non-empty.
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
