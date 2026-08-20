using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Customize.Models;

public class CustomizeSectionInfo : ISectionInfo
{
    public string Key { get; }

    public string IconGlyphKey { get; }

    public string DisplayName { get; }

    public string ModuleId { get; }

    public CustomizeSectionInfo(string key, string iconGlyphKey, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
