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
}
