using Winhance.Core.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.Models;

public class OptimizeSectionInfo : ISectionInfo
{
    public string Key { get; }

    public string IconGlyphKey { get; }

    public string DisplayName { get; }

    public string ModuleId { get; }

    public OptimizeSectionInfo(string key, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = (FeatureDefinitions.Get(moduleId) ?? throw new ArgumentOutOfRangeException(nameof(moduleId), moduleId, null)).IconKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
