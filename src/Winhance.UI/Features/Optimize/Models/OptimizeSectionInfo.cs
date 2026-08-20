using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.Models;

public class OptimizeSectionInfo : ISectionInfo
{
    public string Key { get; }

    public string IconGlyphKey { get; }

    public string DisplayName { get; }

    public string ModuleId { get; }

    public OptimizeSectionInfo(string key, string iconGlyphKey, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
