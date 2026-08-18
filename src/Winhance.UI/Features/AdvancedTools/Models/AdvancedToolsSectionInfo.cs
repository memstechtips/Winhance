namespace Winhance.UI.Features.AdvancedTools.Models;

public class AdvancedToolsSectionInfo
{
    public string Key { get; }

    public string IconResourceKey { get; }

    public string DisplayName { get; }

    public AdvancedToolsSectionInfo(string key, string iconResourceKey, string displayName)
    {
        Key = key;
        IconResourceKey = iconResourceKey;
        DisplayName = displayName;
    }
}
