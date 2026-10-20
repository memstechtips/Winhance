using System.Text.Json;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Selections;

// The only code that knows how a ChoiceValue is spelled inside a .winhance ConfigurationItem, in both directions.
// The JSON property names are the file contract; nothing here may rename one.
public static class ConfigFileMapper
{
    // The answer file's own element name, so a password obscured here is one Setup would accept.
    private const string PasswordElementName = "Password";

    public static InputType InputTypeFor(Setting setting) => setting.Control switch
    {
        ControlKind.Toggle => InputType.Toggle,
        ControlKind.CheckBox => InputType.CheckBox,
        ControlKind.TextBox => InputType.TextBox,
        ControlKind.List => InputType.List,
        ControlKind.Selection or ControlKind.KeyedSelection => InputType.Selection,
        ControlKind.Slider => InputType.NumericRange,
        ControlKind.Action => InputType.Action,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting.Control, "Unhandled ControlKind."),
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
        item.SelectedKey = null;
        item.SelectedKeyLabel = null;
        item.Text = null;
        item.Rows = null;
        item.SavePasswords = null;
        item.File = null;

        switch (value)
        {
            case ChoiceValue.Toggle t:
                item.IsSelected = t.On;
                break;
            case ChoiceValue.CheckBox c:
                item.IsSelected = c.Checked;
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
            case ChoiceValue.Keyed k:
                item.SelectedKey = k.Key;
                item.SelectedKeyLabel = k.Label;
                if (setting.Options?.Source == OptionSource.PowerPlans)
                {
                    // Winhance 26.06.12 reads only the PowerPlan spelling, and reads it in English.
                    item.PowerPlanGuid = k.Key;
                    item.PowerPlanName = PowerPlanCatalog.BuiltInPowerPlans
                        .FirstOrDefault(p => string.Equals(p.Guid, k.Key, StringComparison.OrdinalIgnoreCase))?.Name ?? k.Label;
                }
                break;
            case ChoiceValue.Text text:
                item.Text = text.Value;
                break;
            // A row with no password leaves the field out: base64 of the bare suffix would be an empty password.
            case ChoiceValue.List list:
                item.Rows = list.Rows.Select(row => Obscured(row, list.SavePasswords, setting)).ToList();
                item.SavePasswords = list.SavePasswords;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unhandled ChoiceValue.");
        }
    }

    public static ChoiceValue? DecodeValue(Setting setting, ConfigurationItem item)
    {
        switch (setting.Control)
        {
            case ControlKind.Toggle:
            case ControlKind.Action:
                return item.IsSelected is { } on ? new ChoiceValue.Toggle(on) : null;

            case ControlKind.CheckBox:
                return item.IsSelected is { } isChecked ? new ChoiceValue.CheckBox(isChecked) : null;

            case ControlKind.Slider:
                if (item.PowerSettings is null) return null;
                // Files written from a desktop can carry a null DCValue next to a real ACValue; the import
                // applies the AC value to both contexts then.
                if (TryInt(item.PowerSettings, "ACValue", out var acV))
                    return new ChoiceValue.AcDcNumber(acV, TryInt(item.PowerSettings, "DCValue", out var dcV) ? dcV : acV);
                if (TryInt(item.PowerSettings, "Value", out var v))
                    return new ChoiceValue.Number(v);
                return null;

            case ControlKind.KeyedSelection:
                // The key is kept as written even when this PC does not offer it; the card marks it unavailable.
                var savedKey = item.SelectedKey is { Length: > 0 } selected ? selected : item.PowerPlanGuid;
                if (savedKey is not { Length: > 0 })
                    return null;
                var savedLabel = item.SelectedKeyLabel is { Length: > 0 } label ? label
                    : item.PowerPlanName is { Length: > 0 } name ? name
                    : savedKey;
                return new ChoiceValue.Keyed(savedKey, savedLabel);

            case ControlKind.Selection:
                if (item.CustomStateValues is { Count: > 0 } custom)
                    return new ChoiceValue.CustomValues(Unwrap(custom));
                if (item.PowerSettings is not null
                    && TryInt(item.PowerSettings, "ACIndex", out var acI) && TryInt(item.PowerSettings, "DCIndex", out var dcI))
                    return new ChoiceValue.AcDcOption(acI, dcI);
                if (item.SelectedIndex is { } index)
                    return new ChoiceValue.Option(index);
                return null;   // toggle-era file entry for a setting that became a Selection: the caller decides

            case ControlKind.TextBox:
                // An empty box is a value the user chose; only a missing entry is no answer.
                return item.Text is { } text ? new ChoiceValue.Text(text) : null;

            case ControlKind.List:
                // With no SavePasswords in the file, a row that brought a password ticks the box again.
                if (item.Rows is null) return null;
                var rows = item.Rows.Select(row => Deobscured(row, setting)).ToList();
                return new ChoiceValue.List(
                    rows,
                    item.SavePasswords ?? rows.Any(r => PasswordFields(setting).Any(f => r.Values.GetValueOrDefault(f.Key, string.Empty).Length > 0)));

            default:
                return null;
        }
    }

    public static WinhanceConfigFile ToFile(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature)
    {
        var file = new WinhanceConfigFile();
        var choicesById = set.Settings.ToDictionary(c => c.SettingId, c => c.Value);
        var filesById = new Dictionary<string, CarriedFile>(StringComparer.Ordinal);
        foreach (var carried in set.Files)
            filesById[carried.SettingId] = carried;
        var optimize = new Dictionary<string, ConfigSection>();
        var customize = new Dictionary<string, ConfigSection>();
        var autounattend = new Dictionary<string, ConfigSection>();

        foreach (var (featureId, settings) in byFeature)
        {
            var items = new List<ConfigurationItem>();
            foreach (var setting in settings)
            {
                if (!choicesById.TryGetValue(setting.Id, out var value)) continue;
                // Name holds the localization key, not a translation, so a file reads the same in every language.
                var item = new ConfigurationItem { Id = setting.Id, Name = setting.Display.Name.Value };
                WriteValue(item, setting, value);
                if (filesById.TryGetValue(setting.Id, out var carried))
                    item.File = new CarriedFileItem { Destination = carried.Destination, Base64 = carried.Base64 };
                items.Add(item);
            }
            if (items.Count == 0) continue;

            var section = new ConfigSection { IsIncluded = true, Items = items };
            if (FeatureDefinitions.OptimizeFeatures.Contains(featureId)) optimize[featureId] = section;
            else if (FeatureDefinitions.CustomizeFeatures.Contains(featureId)) customize[featureId] = section;
            else if (FeatureDefinitions.AutounattendFeatures.Contains(featureId)) autounattend[featureId] = section;
        }

        file.Optimize = new FeatureGroupSection { IsIncluded = optimize.Count > 0, Features = optimize };
        file.Customize = new FeatureGroupSection { IsIncluded = customize.Count > 0, Features = customize };
        file.Autounattend = new FeatureGroupSection { IsIncluded = autounattend.Count > 0, Features = autounattend };
        file.WindowsApps = new ConfigSection { IsIncluded = true, Items = set.WindowsApps.Select(AppItem).ToList() };
        file.ExternalApps = new ConfigSection { IsIncluded = true, Items = set.ExternalApps.Select(AppItem).ToList() };
        return file;
    }

    public static SelectionSet FromFile(WinhanceConfigFile file, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature)
    {
        var settingsById = byFeature.Values.SelectMany(s => s).ToDictionary(s => s.Id, s => s);
        var choices = new List<SettingChoice>();
        var files = new List<CarriedFile>();
        foreach (var section in file.Optimize.Features.Values
            .Concat(file.Customize.Features.Values)
            .Concat(file.Autounattend.Features.Values))
        {
            foreach (var item in section.Items)
            {
                if (!settingsById.TryGetValue(SettingIdAliases.Normalize(item.Id), out var setting)) continue;
                if (item.File is { Base64.Length: > 0 } carried)
                    files.Add(new CarriedFile(setting.Id, carried.Destination, carried.Base64));
                if (DecodeValue(setting, item) is { } value)
                    choices.Add(new SettingChoice(setting.Id, value));
            }
        }
        return new SelectionSet(
            choices,
            file.WindowsApps.Items.Select(AppChoiceOf).ToList(),
            file.ExternalApps.Items.Select(AppChoiceOf).ToList())
        {
            Files = files,
        };
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

    private static IEnumerable<Field> PasswordFields(Setting setting) =>
        setting.List?.Fields.Where(f => f.Kind == FieldKind.Password) ?? Enumerable.Empty<Field>();

    private static Dictionary<string, string> Obscured(ChoiceValue.ListRow row, bool savePasswords, Setting setting)
    {
        var values = new Dictionary<string, string>(row.Values, StringComparer.Ordinal);
        foreach (var field in PasswordFields(setting))
        {
            if (savePasswords && values.GetValueOrDefault(field.Key, string.Empty) is { Length: > 0 } typed)
                values[field.Key] = AccountPasswords.Obscure(typed, PasswordElementName);
            else
                values.Remove(field.Key);
        }
        return values;
    }

    private static ChoiceValue.ListRow Deobscured(Dictionary<string, string> row, Setting setting)
    {
        var values = new Dictionary<string, string>(row, StringComparer.Ordinal);
        foreach (var field in PasswordFields(setting))
        {
            values[field.Key] =
                AccountPasswords.TryDeobscure(values.GetValueOrDefault(field.Key), PasswordElementName, out var typed)
                    ? typed
                    : string.Empty;
        }
        return new ChoiceValue.ListRow(values);
    }

    private static AppChoice AppChoiceOf(ConfigurationItem item) =>
        new(item.Id, item.Name, item.AppxPackageName, item.CapabilityName, item.OptionalFeatureName, item.WinGetPackageId);

    // JSON round-trips box numbers as JsonElement, so the file is read through Convert.
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

    // The runtime types the import bridge produces (int before long before double), so the apply pipeline
    // compares like with like.
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
