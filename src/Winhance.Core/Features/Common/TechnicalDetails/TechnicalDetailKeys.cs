namespace Winhance.Core.Features.Common.TechnicalDetails;

// A test reflects over these constants and asserts each one exists in en.json and in all 29 language files.
public static class TechnicalDetailKeys
{
    public const string SectionOptions = "TechnicalDetails_Section_Options";
    public const string SectionOptionsDescription = "TechnicalDetails_Section_Options_Description";
    public const string SectionScripts = "TechnicalDetails_Section_Scripts";
    public const string SectionScriptsDescription = "TechnicalDetails_Section_Scripts_Description";
    public const string SectionRegContent = "TechnicalDetails_Section_RegContent";
    public const string SectionRegContentDescription = "TechnicalDetails_Section_RegContent_Description";
    public const string SectionPowerPlansDescription = "TechnicalDetails_Section_PowerPlans_Description";

    public const string ColumnOption = "TechnicalDetails_Column_Option";
    public const string ColumnRole = "TechnicalDetails_Column_Role";

    // Captions naming what each part of the table is showing. Without them the panel puts a path, a
    // value name and value data on screen and leaves the reader to infer which is which.
    public const string LabelPath = "TechnicalDetails_Label_Path";
    public const string LabelValueName = "TechnicalDetails_Label_ValueName";
    public const string LabelValueType = "TechnicalDetails_Label_ValueType";
    public const string LabelTask = "TechnicalDetails_Label_Task";
    public const string ColumnPowerValue = "TechnicalDetails_Column_PowerValue";

    // One line per group saying what Winhance does with those locations. Without it "Registry" is a
    // noun with no verb: the reader cannot tell whether the values are read, written, or both.
    public const string DescRegistry = "TechnicalDetails_Desc_Registry";
    public const string DescScheduledTask = "TechnicalDetails_Desc_ScheduledTask";
    public const string DescPower = "TechnicalDetails_Desc_Power";

    public const string GroupRegistry = "TechnicalDetails_Group_Registry";
    public const string GroupPower = "TechnicalDetails_Group_Power";
    public const string GroupPowerPlan = "TechnicalDetails_Group_PowerPlan";
    public const string ColumnPowerPlanScheme = "TechnicalDetails_Column_PowerPlanScheme";
    public const string ColumnPowerPlanStatus = "TechnicalDetails_Column_PowerPlanStatus";

    // A numeric setting takes any number in a range rather than a fixed list, so the range itself is
    // the thing to state. {0} is a bare "min-max units" string, kept language-neutral with a dash.
    public const string ChipNumericRange = "TechnicalDetails_Chip_NumericRange";
    public const string ChipNumericRangeTooltip = "TechnicalDetails_Chip_NumericRange_Tooltip";
    public const string GroupScheduledTask = "TechnicalDetails_Group_ScheduledTask";
    public const string GroupAlsoRuns = "TechnicalDetails_Group_AlsoRuns";
    public const string ColumnScript = "TechnicalDetails_Column_Script";
    public const string ColumnRegFile = "TechnicalDetails_Column_RegFile";
    public const string DefaultValueName = "TechnicalDetails_DefaultValueName";
    public const string DefaultValueNameTooltip = "TechnicalDetails_DefaultValueName_Tooltip";

    // The live-readings row, shown only when detection matched no option.
    public const string ReadingCustom = "TechnicalDetails_Reading_Custom";
    public const string ReadingMalformed = "TechnicalDetails_Reading_Malformed";
    public const string ReadingUndetermined = "TechnicalDetails_Reading_Undetermined";
    public const string ReadingUnreadable = "TechnicalDetails_Reading_Unreadable";
    public const string ReadingAbsent = "TechnicalDetails_Reading_Absent";

    public const string Current = "TechnicalDetails_Current";
    public const string Recommended = "TechnicalDetails_Recommended";
    public const string Default = "TechnicalDetails_DefaultValue";
    public const string CurrentTooltip = "TechnicalDetails_Current_Tooltip";
    public const string RecommendedTooltip = "TechnicalDetails_Recommended_Tooltip";
    public const string DefaultTooltip = "TechnicalDetails_DefaultValue_Tooltip";
    public const string OnApply = "TechnicalDetails_Script_OnApply";
    public const string OrNotSet = "TechnicalDetails_OrNotSet";
    public const string DeletesKey = "TechnicalDetails_DeletesKey";
    public const string OpenRegedit = "TechnicalDetails_OpenRegedit";

    public const string ChipGroupPolicy = "TechnicalDetails_Chip_GroupPolicy";
    public const string ChipGroupPolicyTooltip = "TechnicalDetails_Chip_GroupPolicy_Tooltip";
    public const string ChipDetectionOnly = "TechnicalDetails_Chip_DetectionOnly";
    public const string ChipDetectionOnlyTooltip = "TechnicalDetails_Chip_DetectionOnly_Tooltip";
    public const string ChipApplyOnly = "TechnicalDetails_Chip_ApplyOnly";
    public const string ChipApplyOnlyTooltip = "TechnicalDetails_Chip_ApplyOnly_Tooltip";
    // No Mirrored chip: the group header lists every path the value is written to, each with its own
    // button, which is a better answer than a chip saying "there is more than one place".
    public const string ChipPerNetworkInterface = "TechnicalDetails_Chip_PerNetworkInterface";
    public const string ChipPerNetworkInterfaceTooltip = "TechnicalDetails_Chip_PerNetworkInterface_Tooltip";
    public const string ChipPerMonitor = "TechnicalDetails_Chip_PerMonitor";
    public const string ChipPerMonitorTooltip = "TechnicalDetails_Chip_PerMonitor_Tooltip";
    public const string ChipPartOfValue = "TechnicalDetails_Chip_PartOfValue";
    public const string ChipPartOfValueTooltip = "TechnicalDetails_Chip_PartOfValue_Tooltip";
    public const string ChipSubKey = "TechnicalDetails_Chip_SubKey";
    public const string ChipSubKeyTooltip = "TechnicalDetails_Chip_SubKey_Tooltip";
    public const string ChipOsSpecific = "TechnicalDetails_Chip_OsSpecific";
    public const string ChipOsSpecificTooltip = "TechnicalDetails_Chip_OsSpecific_Tooltip";
    public const string ChipEnablementKey = "TechnicalDetails_Chip_EnablementKey";
    public const string ChipEnablementKeyTooltip = "TechnicalDetails_Chip_EnablementKey_Tooltip";
    public const string ChipHardwareControlled = "TechnicalDetails_Chip_HardwareControlled";
    public const string ChipHardwareControlledTooltip = "TechnicalDetails_Chip_HardwareControlled_Tooltip";

    // Apply-time side effects surfaced in "Before you apply"
    public const string NotesHeading = "TechnicalDetails_Notes_Heading";

    // Used instead when the setting asks first: then these happen only if you agree.
    public const string NotesHeadingConditional = "TechnicalDetails_Notes_Heading_Conditional";
    public const string NotesDetailHeader = "TechnicalDetails_Notes_DetailHeader";

    // Heads the band under the grid that lists, per option, which other settings that option changes.
    public const string OptionLinksHeading = "TechnicalDetails_OptionLinks_Heading";

    public const string EffectRegistryWrite = "TechnicalDetails_Effect_RegistryWrite";
    public const string EffectNativePower = "TechnicalDetails_Effect_NativePower";
    public const string EffectWallpaper = "TechnicalDetails_Effect_Wallpaper";

    public const string TaskEnabled = "TechnicalDetails_Task_Enabled";
    public const string TaskDisabled = "TechnicalDetails_Task_Disabled";

    public const string PowerCfgSubgroup = "TechnicalDetails_PowerCfg_Subgroup";
    public const string PowerCfgSetting = "TechnicalDetails_PowerCfg_Setting";
    // Shared with the AC/DC controls on the card itself, so the panel says the same thing they do.
    public const string PowerPluggedIn = "PowerStatus_PluggedIn";
    public const string PowerOnBattery = "PowerStatus_OnBattery";
    public const string PowerPlanInstalled = "TechnicalDetails_PowerPlan_Installed";
    public const string PowerPlanNotInstalled = "TechnicalDetails_PowerPlan_NotInstalled";

    public const string CodeWhenSetTo = "TechnicalDetails_Code_WhenSetTo";

    public const string RelRequires = "TechnicalDetails_Rel_Requires";
    public const string RelEnables = "TechnicalDetails_Rel_Enables";
    public const string RelControls = "TechnicalDetails_Rel_Controls";

    public const string RelSetAutomatically = "TechnicalDetails_Rel_SetAutomatically";

    public const string ApplyConfirmation = "TechnicalDetails_Apply_Confirmation";
    public const string ApplyConfirmationDetail = "TechnicalDetails_Apply_ConfirmationDetail";
    public const string ApplyReboot = "TechnicalDetails_Apply_Reboot";
    public const string ApplyRebootDetail = "TechnicalDetails_Apply_RebootDetail";

    // The requirement chips. Separate from the two above because these take the process or service
    // name as {0}, and because the process wording had to stop claiming Winhance restarts it for you.
    public const string ApplyRestartChip = "TechnicalDetails_Apply_RestartChip";
    public const string ApplyRestartChipDeferred = "TechnicalDetails_Apply_RestartChip_Deferred";
    public const string ApplyRestartChipService = "TechnicalDetails_Apply_RestartChip_Service";

    public const string On = "Common_On";
    public const string Off = "Common_Off";
}
