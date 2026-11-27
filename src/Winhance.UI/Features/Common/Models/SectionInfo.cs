using Winhance.Core.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

public class SectionInfo : ISectionInfo
{
    public string Key { get; }

    public string IconGlyphKey { get; }

    public string ModuleId { get; }

    public SectionInfo(string key, string moduleId)
    {
        Key = key;
        IconGlyphKey = (FeatureDefinitions.Get(moduleId) ?? throw new ArgumentOutOfRangeException(nameof(moduleId), moduleId, null)).IconKey;
        ModuleId = moduleId;
    }
}
