using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>One comparison result for a single setting: the existing app's detected/applied state
/// vs the new catalog engine's, and whether they agree. Shared with <see cref="ApplyEquivalenceHarness"/>.</summary>
public sealed record EquivalenceRow(string Id, string OldState, string NewState, bool Match);

/// <summary>Mechanism-classification predicates for a <see cref="SettingDefinition"/> - "is this a pure registry
/// toggle / registry selection / powercfg selection / powercfg numeric / scheduled-task toggle" (a setting whose
/// DETECTION comes from a single mechanism, with apply-only effects allowed). Retained after old-discovery retirement
/// because the surviving apply-equivalence harness still classifies settings by these predicates; the old
/// detection-equivalence runner methods (which drove old discovery) were removed with the discovery service.</summary>
public static class RegistryToggleEquivalenceHarness
{
    /// <summary>True when a toggle's DETECTION is registry-based. Apply-only effects (PowerShell scripts, .reg
    /// blobs, native-power API) are ALLOWED - per the design they ride along on apply and do not change
    /// detection, which always comes from the registry Set. Only non-registry DETECTION is excluded: a
    /// combobox, powercfg (deferred), a scheduled task, or a custom DetectionType.</summary>
    public static bool IsPureRegistryToggle(SettingDefinition def)
    {
        if (def.InputType != InputType.Toggle && def.InputType != InputType.CheckBox)
            return false;
        if (def.RegistrySettings.Count == 0)
            return false;
        if (def.ComboBox != null)
            return false;
        if (def.PowerCfgSettings is { Count: > 0 })
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a selection's (ComboBox) DETECTION is registry-based. Apply-only effects (PowerShell
    /// scripts, .reg blobs, native-power API) are ALLOWED - they do not change detection. The selection
    /// analogue of <see cref="IsPureRegistryToggle"/>.</summary>
    public static bool IsPureRegistrySelection(SettingDefinition def)
    {
        if (def.InputType != InputType.Selection)
            return false;
        if (def.ComboBox?.Options is not { Count: > 0 })
            return false;
        if (def.RegistrySettings.Count == 0)
            return false;
        if (def.PowerCfgSettings is { Count: > 0 })
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a selection's (ComboBox) DETECTION is powercfg-based: it carries powercfg settings
    /// AND a ComboBox (so it maps the AC value index to an option). Excludes the power-plan selection (which has
    /// no ComboBox) and anything with a registry/scheduled-task/custom detection mechanism. The powercfg
    /// analogue of <see cref="IsPureRegistrySelection"/>.</summary>
    public static bool IsPurePowerCfgSelection(SettingDefinition def)
    {
        if (def.InputType != InputType.Selection)
            return false;
        if (def.PowerCfgSettings is not { Count: > 0 })
            return false;
        if (def.ComboBox?.Options is not { Count: > 0 })
            return false; // excludes power-plan-selection (no ComboBox)
        if (def.RegistrySettings.Count > 0)
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a numeric (slider) setting's DETECTION is powercfg-based - it carries powercfg settings
    /// and no registry/scheduled-task/custom detection mechanism. A numeric has no ComboBox; its value IS the
    /// raw AC value index.</summary>
    public static bool IsPurePowerCfgNumeric(SettingDefinition def)
    {
        if (def.InputType != InputType.NumericRange)
            return false;
        if (def.PowerCfgSettings is not { Count: > 0 })
            return false;
        if (def.RegistrySettings.Count > 0)
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a definition is a pure scheduled-task toggle - a single ScheduledTaskSetting and no
    /// other mechanism (no registry, combobox, powercfg, script, .reg, native-power, custom detector).</summary>
    public static bool IsPureScheduledTaskToggle(SettingDefinition def)
    {
        if (def.InputType != InputType.Toggle && def.InputType != InputType.CheckBox)
            return false;
        if (def.ScheduledTaskSettings.Count == 0)
            return false;
        if (def.RegistrySettings.Count > 0)
            return false;
        if (def.ComboBox != null)
            return false;
        if (def.PowerCfgSettings is { Count: > 0 })
            return false;
        if (def.PowerShellScripts.Count > 0)
            return false;
        if (def.RegContents.Count > 0)
            return false;
        if (def.NativePowerApiSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }
}
