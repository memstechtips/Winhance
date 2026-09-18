using Winhance.Core.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Customize.Models;

public class CustomizeSectionInfo : ISectionInfo
{
    public string Key { get; }

    public string IconGlyphKey { get; }

    public string DisplayName { get; }

    public string ModuleId { get; }

    public CustomizeSectionInfo(string key, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = (FeatureDefinitions.Get(moduleId) ?? throw new ArgumentOutOfRangeException(nameof(moduleId), moduleId, null)).IconKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
