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

    // Script / RegContent labels
    public string ScriptOnEnable        { get; init; } = "On Enable";
    public string ScriptOnDisable       { get; init; } = "On Disable";
    public string RegContentOnEnable    { get; init; } = "On Enable";
    public string RegContentOnDisable   { get; init; } = "On Disable";

    // Dependency relation
    public string DependencyEquals      { get; init; } = "=";
    public string DependencyNotEquals   { get; init; } = "≠";
}
