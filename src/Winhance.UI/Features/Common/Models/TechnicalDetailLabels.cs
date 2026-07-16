namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Localized label strings for the Technical Details panel.
/// </summary>
public record TechnicalDetailLabels
{
    public string Path { get; init; } = "Path";
    public string Value { get; init; } = "Value";
    public string Current { get; init; } = "Current";
    public string Recommended { get; init; } = "Recommended";
    public string Default { get; init; } = "Default";
    public string ValueNotExist { get; init; } = "doesn't exist";
    public string On { get; init; } = "On";
    public string Off { get; init; } = "Off";

    // Section headers
    public string SectionRegistry       { get; init; } = "Registry Changes";
    public string SectionScheduledTasks { get; init; } = "Scheduled Tasks";
    public string SectionPowerSettings  { get; init; } = "Power Settings";
    public string SectionScripts        { get; init; } = "PowerShell Scripts";
    public string SectionRegContent     { get; init; } = "Registry Content";
    public string SectionDependencies   { get; init; } = "Depends On";
    public string SectionOptions        { get; init; } = "Options";
    public string SectionTargets        { get; init; } = "Targets";
    public string SectionEffects        { get; init; } = "Effects";
    public string SectionRelationships  { get; init; } = "Relationships";

    // Option->value table suffixes: a choice that also accepts the key being absent, or that deletes the key.
    public string OrNotSet              { get; init; } = "or not set";
    public string DeletesKey           { get; init; } = "deletes key";

    // Targets section
    public string TargetRegistry        { get; init; } = "Registry";
    public string TargetPower           { get; init; } = "Power";
    public string TargetTask            { get; init; } = "Scheduled Task";
    public string MetaGroupPolicy       { get; init; } = "Group Policy";

    // Effects section
    public string EffectRegistryWrite   { get; init; } = "Writes registry value";
    public string EffectNativePower     { get; init; } = "Native power write";
    public string EffectWallpaper       { get; init; } = "Sets desktop wallpaper";

    // Relationships section
    public string RelRequires           { get; init; } = "Requires";
    public string RelEnables            { get; init; } = "Enables";
    public string RelControls           { get; init; } = "Sets";
    public string RelNestedUnder        { get; init; } = "Nested under";

    // Script / RegContent labels
    public string ScriptOnEnable        { get; init; } = "On Enable";
    public string ScriptOnDisable       { get; init; } = "On Disable";
    // Used for one-shot Action settings (e.g. "Clean Start Menu") where the script runs once
    // on click and has no reverse direction — "On Enable" / "On Disable" framing doesn't fit.
    public string ScriptOnApply         { get; init; } = "On Apply";
    public string RegContentOnEnable    { get; init; } = "On Enable";
    public string RegContentOnDisable   { get; init; } = "On Disable";

    // Dependency relation
    public string DependencyEquals      { get; init; } = "=";
    public string DependencyNotEquals   { get; init; } = "≠";

    // PowerConfig labels
    public string PowerCfgSubgroup { get; init; } = "Subgroup";
    public string PowerCfgSetting  { get; init; } = "Setting";
}
