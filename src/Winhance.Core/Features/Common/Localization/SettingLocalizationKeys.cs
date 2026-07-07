using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Localization;

/// <summary>
/// Pure, dependency-free builder for the localization-key strings a
/// <see cref="SettingDefinition"/> resolves at runtime. The key formats here MUST stay
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
    /// Generic localized "Custom" state key used by every Selection setting on a state mismatch
    /// when no per-setting override key exists.
    /// </summary>
    public const string CommonCustomState = "Common_CustomState";

    private static string Base(SettingDefinition setting) => setting.LocalizationId ?? setting.Id;

    // Catalog-Setting base (Slice C/D foundation): the catalog Id IS the canonical, alias-normalized id, so it
    // equals the def base LocalizationId ?? Id for every paired setting (proven by LocalizeDisplayReadSwapEquivalence
    // Tests + locked here by SettingLocalizationKeysCatalogEquivalenceTests). Lets the key-builders run off a catalog
    // Setting instead of a SettingDefinition; the def overloads stay live until the apply-cluster / loc-key port.
    private static string Base(Setting setting) => setting.Id;

    /// <summary><c>Setting_{LocalizationId ?? Id}_Name</c></summary>
    public static string Name(SettingDefinition setting) => $"Setting_{Base(setting)}_Name";

    /// <summary><c>Setting_{LocalizationId ?? Id}_Description</c></summary>
    public static string Description(SettingDefinition setting) => $"Setting_{Base(setting)}_Description";

    /// <summary><c>Setting_{LocalizationId ?? Id}_Option_{index}</c></summary>
    public static string OptionDisplay(SettingDefinition setting, int index) => $"Setting_{Base(setting)}_Option_{index}";

    /// <summary><c>Setting_{LocalizationId ?? Id}_OptionTooltip_{index}</c></summary>
    public static string OptionTooltip(SettingDefinition setting, int index) => $"Setting_{Base(setting)}_OptionTooltip_{index}";

    /// <summary><c>Setting_{LocalizationId ?? Id}_OptionWarning_{index}</c></summary>
    public static string OptionWarning(SettingDefinition setting, int index) => $"Setting_{Base(setting)}_OptionWarning_{index}";

    /// <summary><c>Setting_{LocalizationId ?? Id}_Option_Custom</c> — per-setting Custom-state override.</summary>
    public static string OptionCustom(SettingDefinition setting) => $"Setting_{Base(setting)}_Option_Custom";

    // ---- Catalog-Setting overloads (Slice C/D foundation; additive, keyed off the catalog Id which == the def base
    // for every paired setting). ExpectedKeys(Setting) is deferred to the LocalizationKeyReferenceTests port (it walks
    // the catalog States and needs its own set-equivalence proof). ----
    public static string Name(Setting setting) => $"Setting_{Base(setting)}_Name";
    public static string Description(Setting setting) => $"Setting_{Base(setting)}_Description";
    public static string OptionDisplay(Setting setting, int index) => $"Setting_{Base(setting)}_Option_{index}";
    public static string OptionTooltip(Setting setting, int index) => $"Setting_{Base(setting)}_OptionTooltip_{index}";
    public static string OptionWarning(Setting setting, int index) => $"Setting_{Base(setting)}_OptionWarning_{index}";
    public static string OptionCustom(Setting setting) => $"Setting_{Base(setting)}_Option_Custom";

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
    /// The COMPLETE set of localization keys this setting will actually request at runtime,
    /// applying the same conditionals as the service:
    /// <list type="bullet">
    /// <item>Name and Description are always requested.</item>
    /// <item>Group keys (both compact and snake-case variants) only when <c>GroupName != null</c>.</item>
    /// <item>For a ComboBox setting: the per-setting Custom override key and <c>Common_CustomState</c>;
    /// per option, the per-setting option-display key (only when the display name is NOT already a
    /// localization key), the option-tooltip key (only when the option has a non-empty tooltip), and
    /// the option-warning key (only when the option has a non-empty warning).</item>
    /// </list>
    /// Both group variants are returned; a consumer treats the group as covered if ANY variant exists.
    /// </summary>
    public static IEnumerable<string> ExpectedKeys(SettingDefinition setting)
    {
        yield return Name(setting);
        yield return Description(setting);

        if (setting.GroupName != null)
        {
            yield return GroupCompact(setting.GroupName);
            yield return GroupSnake(setting.GroupName);
        }

        if (setting.ComboBox != null)
        {
            yield return OptionCustom(setting);
            yield return CommonCustomState;

            var options = setting.ComboBox.Options;
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    var option = options[i];

                    if (!IsLocalizationKey(option.DisplayName))
                    {
                        yield return OptionDisplay(setting, i);
                    }

                    if (!string.IsNullOrEmpty(option.Tooltip))
                    {
                        yield return OptionTooltip(setting, i);
                    }

                    if (!string.IsNullOrEmpty(option.Warning))
                    {
                        yield return OptionWarning(setting, i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Catalog-Setting overload of <see cref="ExpectedKeys(SettingDefinition)"/> (Slice C/D loc-key port):
    /// the COMPLETE set of localization keys this setting requests at runtime, reproducing the def version off
    /// the catalog homes. Name/Description always; group keys (compact + snake) when <c>Display.GroupName != null</c>;
    /// the option block (per-setting Custom override + <c>Common_CustomState</c> + per-state option-display/tooltip/
    /// warning) for a Selection setting - the catalog equivalent of the def's <c>ComboBox != null</c> (a Toggle,
    /// Slider, Action, or dynamic PowerPlan carries no enumerated option keys, matching def <c>ComboBox == null</c>).
    /// Per state: OptionDisplay only when the label is NOT itself a localization key; OptionTooltip/OptionWarning only
    /// when the state carries a non-empty tooltip/warning. Set-equivalent to <see cref="ExpectedKeys(SettingDefinition)"/>
    /// over the whole paired population (proven by SettingLocalizationKeysCatalogEquivalenceTests).
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
            yield return OptionCustom(setting);
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
