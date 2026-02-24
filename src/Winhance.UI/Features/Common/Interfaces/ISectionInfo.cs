namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Common interface for section metadata used by section-based page ViewModels.
/// </summary>
public interface ISectionInfo
{
    string Key { get; }
    string IconGlyphKey { get; }
    string DisplayName { get; }
    string ModuleId { get; }
}
