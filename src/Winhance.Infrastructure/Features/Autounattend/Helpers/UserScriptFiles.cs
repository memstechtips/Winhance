using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.Autounattend.Helpers;

internal sealed record UserScriptFile(RunContext Run, string Name, string Content)
{
    public string Path => ScriptPaths.UnattendUserScriptsDirectory + @"\" + Name;

    public string Extension => Name[Name.LastIndexOf('.')..];
}

// Each script travels as a file of its own, exactly as typed, so nothing in it can collide with the PowerShell
// that runs it. The extractor writes each extension in the encoding its runner reads (.reg as UTF-16).
internal static class UserScriptFiles
{
    // One per option of the script cards' type field, in the same order.
    internal static readonly string[] Extensions = [".ps1", ".cmd", ".bat", ".reg"];

    public static IReadOnlyList<UserScriptFile> From(SelectionSet set) =>
        [.. SettingCatalog.All.SelectMany(setting => setting.Targets.OfType<AutounattendScript>()
            .SelectMany(target => FilesFor(set, setting.Id, target.Run)))];

    private static IEnumerable<UserScriptFile> FilesFor(SelectionSet set, string settingId, RunContext run)
    {
        if (set.Value(settingId) is not ChoiceValue.List list)
            yield break;

        var number = 0;
        foreach (var row in list.Rows)
        {
            var script = row.Values.GetValueOrDefault("script", string.Empty).Trim();
            if (script.Length == 0)
                continue;

            var type = int.TryParse(row.Values.GetValueOrDefault("type"), out var index)
                && index >= 0 && index < Extensions.Length ? index : 0;
            yield return new UserScriptFile(run, $"{run}-{++number}{Extensions[type]}", script);
        }
    }
}
