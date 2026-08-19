using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Selections;

// The only code that knows how a ChoiceValue is spelled inside a .winhance ConfigurationItem, in both directions.
// The JSON property names are the file contract; nothing here may rename one.
public static class ConfigFileMapper
{
    public static InputType InputTypeFor(Setting setting) => setting.Control switch
    {
        ControlKind.Selection or ControlKind.PowerPlan => InputType.Selection,
        ControlKind.Slider => InputType.NumericRange,
        ControlKind.Action => InputType.Action,
        _ => InputType.Toggle,
    };

    public static void WriteValue(ConfigurationItem item, Setting setting, ChoiceValue value)
    {
        item.InputType = InputTypeFor(setting);
        item.IsSelected = null;
        item.SelectedIndex = null;
        item.CustomStateValues = null;
        item.PowerSettings = null;
        item.PowerPlanGuid = null;
        item.PowerPlanName = null;

        switch (value)
        {
            case ChoiceValue.Toggle t:
                item.IsSelected = t.On;
                break;
            case ChoiceValue.Option o:
                item.SelectedIndex = o.Index;
                break;
            case ChoiceValue.CustomValues c:
                item.CustomStateValues = new Dictionary<string, object>(c.Values);
                break;
            case ChoiceValue.AcDcOption a:
                item.PowerSettings = new Dictionary<string, object> { ["ACIndex"] = a.AcIndex, ["DCIndex"] = a.DcIndex };
                break;
            case ChoiceValue.Number n:
                item.PowerSettings = new Dictionary<string, object> { ["Value"] = n.Value };
                break;
            case ChoiceValue.AcDcNumber an:
                item.PowerSettings = new Dictionary<string, object> { ["ACValue"] = an.Ac, ["DCValue"] = an.Dc };
                break;
            case ChoiceValue.PowerPlan p:
                item.PowerPlanGuid = p.Guid;
                item.PowerPlanName = p.Name;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unhandled ChoiceValue.");
        }
    }

    public static ChoiceValue? DecodeValue(Setting setting, ConfigurationItem item)
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
            return string.IsNullOrEmpty(item.PowerPlanGuid) ? null : new ChoiceValue.PowerPlan(item.PowerPlanGuid, item.PowerPlanName ?? "Unknown");

        switch (setting.Control)
        {
            case ControlKind.Toggle:
            case ControlKind.Action:
                return item.IsSelected is { } on ? new ChoiceValue.Toggle(on) : null;

            case ControlKind.Slider:
                if (item.PowerSettings is null) return null;
                // Files written from a desktop can carry a null DCValue next to a real ACValue; the import has
                // always applied the AC value to both contexts in that case.
                if (TryInt(item.PowerSettings, "ACValue", out var acV))
                    return new ChoiceValue.AcDcNumber(acV, TryInt(item.PowerSettings, "DCValue", out var dcV) ? dcV : acV);
                if (TryInt(item.PowerSettings, "Value", out var v))
                    return new ChoiceValue.Number(v);
                return null;

            case ControlKind.Selection:
                if (item.CustomStateValues is { Count: > 0 } custom)
                    return new ChoiceValue.CustomValues(Unwrap(custom));
                if (item.PowerSettings is not null
                    && TryInt(item.PowerSettings, "ACIndex", out var acI) && TryInt(item.PowerSettings, "DCIndex", out var dcI))
                    return new ChoiceValue.AcDcOption(acI, dcI);
                if (item.SelectedIndex is { } index)
                    return new ChoiceValue.Option(index);
                return null;   // toggle-era file entry for a setting that became a Selection: the caller decides

            default:
                return null;
        }
    }

    public static WinhanceConfigFile ToFile(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature)
    {
        var file = new WinhanceConfigFile();
        var choicesById = set.Settings.ToDictionary(c => c.SettingId, c => c.Value);
        var optimize = new Dictionary<string, ConfigSection>();
        var customize = new Dictionary<string, ConfigSection>();

        foreach (var (featureId, settings) in byFeature)
        {
            var items = new List<ConfigurationItem>();
            foreach (var setting in settings)
            {
                if (!choicesById.TryGetValue(setting.Id, out var value)) continue;
                var item = new ConfigurationItem { Id = setting.Id, Name = setting.Display.Name };
                WriteValue(item, setting, value);
                items.Add(item);
            }
            if (items.Count == 0) continue;

            var section = new ConfigSection { IsIncluded = true, Items = items };
            if (FeatureDefinitions.OptimizeFeatures.Contains(featureId)) optimize[featureId] = section;
            else if (FeatureDefinitions.CustomizeFeatures.Contains(featureId)) customize[featureId] = section;
        }

        file.Optimize = new FeatureGroupSection { IsIncluded = optimize.Count > 0, Features = optimize };
        file.Customize = new FeatureGroupSection { IsIncluded = customize.Count > 0, Features = customize };
        file.WindowsApps = new ConfigSection { IsIncluded = true, Items = set.WindowsApps.Select(AppItem).ToList() };
        file.ExternalApps = new ConfigSection { IsIncluded = true, Items = set.ExternalApps.Select(AppItem).ToList() };
        return file;
    }

    public static SelectionSet FromFile(WinhanceConfigFile file, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature)
    {
        var settingsById = byFeature.Values.SelectMany(s => s).ToDictionary(s => s.Id, s => s);
        var choices = new List<SettingChoice>();
        foreach (var section in file.Optimize.Features.Values.Concat(file.Customize.Features.Values))
        {
            foreach (var item in section.Items)
            {
                if (!settingsById.TryGetValue(SettingIdAliases.Normalize(item.Id), out var setting)) continue;
                if (DecodeValue(setting, item) is { } value)
                    choices.Add(new SettingChoice(setting.Id, value));
            }
        }
        return new SelectionSet(
            choices,
            file.WindowsApps.Items.Select(AppChoiceOf).ToList(),
            file.ExternalApps.Items.Select(AppChoiceOf).ToList(),
            AutounattendChoices.None);
    }

    public static ConfigurationItem AppItem(AppChoice app) => new()
    {
        Id = app.Id,
        Name = app.Name,
        IsSelected = true,
        InputType = InputType.Toggle,
        AppxPackageName = app.AppxPackageName,
        CapabilityName = app.CapabilityName,
        OptionalFeatureName = app.OptionalFeatureName,
        WinGetPackageId = app.WinGetPackageId,
    };

    private static AppChoice AppChoiceOf(ConfigurationItem item) =>
        new(item.Id, item.Name, item.AppxPackageName, item.CapabilityName, item.OptionalFeatureName, item.WinGetPackageId);

    // JSON round-trips box numbers as JsonElement; the file has always been read through Convert.
    private static bool TryInt(IReadOnlyDictionary<string, object> dict, string key, out int value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw is null) return false;
        try { value = Convert.ToInt32(raw is JsonElement je ? UnwrapElement(je) : raw); return true; }
        catch { return false; }
    }

    private static IReadOnlyDictionary<string, object> Unwrap(IReadOnlyDictionary<string, object> values)
    {
        var result = new Dictionary<string, object>(values.Count);
        foreach (var (k, v) in values)
            result[k] = v is JsonElement je ? UnwrapElement(je) : v;
        return result;
    }

    // Same runtime types the import bridge has always produced (int before long before double), so the apply
    // pipeline compares the values it compared before.
    private static object UnwrapElement(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.Number when je.TryGetInt32(out var i) => i,
        JsonValueKind.Number when je.TryGetInt64(out var l) => l,
        JsonValueKind.Number => je.GetDouble(),
        JsonValueKind.String => je.GetString()!,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => je,
    };
}
