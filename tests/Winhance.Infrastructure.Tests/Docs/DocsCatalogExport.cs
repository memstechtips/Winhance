using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.TechnicalDetails;

namespace Winhance.Infrastructure.Tests.Docs;

internal sealed record DocsExport(
    int SchemaVersion,
    string WinhanceVersion,
    string CatalogHash,
    int SettingCount,
    DocsReferenceBuilds ReferenceBuilds,
    IReadOnlyList<DocsFeature> Features);

internal sealed record DocsReferenceBuilds(int Win10, int Win11);

internal sealed record DocsFeature(string Id, IReadOnlyList<DocsSetting> Settings);

internal sealed record DocsSetting(
    string Id,
    string Name,
    string Description,
    string? Group,
    string Control,
    bool IsSubjectivePreference,
    string? AddedInVersion,
    string? UiParentId,
    DocsAvailability Availability,
    OptionMatrix? Matrix,
    OptionMatrix? MatrixWin10);

internal sealed record DocsAvailability(
    IReadOnlyList<DocsBuildRange> Builds,
    IReadOnlyList<string> Hardware,
    bool RequiresAdvancedUnlock,
    DocsCompatibility Message);

internal sealed record DocsBuildRange(string Min, string Max);

internal sealed record DocsCompatibility(string? Win10, string? Win11);

// The winhance.net docs render the same OptionMatrix the in-app Technical Details panel renders, so the two can
// never disagree; the export is that matrix per setting, once per reference build, plus the card-level facts.
internal static class DocsCatalogExport
{
    public const int SchemaVersion = 1;
    public static readonly WinBuild Win10 = new(19045);
    public static readonly WinBuild Win11 = new(26100);

    private static readonly JsonSerializerOptions Indented = Options(indented: true);
    private static readonly JsonSerializerOptions Compact = Options(indented: false);

    public static DocsExport Build(ILocalizationService loc, string winhanceVersion)
    {
        var features = SettingCatalog.ByFeature
            .Select(kvp => new DocsFeature(kvp.Key, kvp.Value.Select(s => ExportSetting(s, loc)).ToList()))
            .ToList();

        // Hash covers the features only, so regenerating unchanged data (even under a new version) is a no-op diff.
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(features, Compact))).ToLowerInvariant();

        return new DocsExport(
            SchemaVersion,
            winhanceVersion,
            hash,
            features.Sum(f => f.Settings.Count),
            new DocsReferenceBuilds(Win10.Build, Win11.Build),
            features);
    }

    public static string ToJson(DocsExport export) => JsonSerializer.Serialize(export, Indented);

    public static string ReadCsprojVersion(string solutionDir)
    {
        var csproj = File.ReadAllText(Path.Combine(solutionDir, "src", "Winhance.UI", "Winhance.UI.csproj"));
        var match = Regex.Match(csproj, "<Version>([^<]+)</Version>");
        return match.Success
            ? match.Groups[1].Value.Trim()
            : throw new InvalidOperationException("Winhance.UI.csproj has no <Version>");
    }

    private static DocsSetting ExportSetting(Setting s, ILocalizationService loc)
    {
        var snapshot = Snapshot(s, loc);
        var win11 = TechnicalDetailsBuilder.Build(s, snapshot, loc, Win11);
        var win10 = TechnicalDetailsBuilder.Build(s, snapshot, loc, Win10);
        // Record equality compares the list members by reference, so the JSON is the only honest comparison.
        var sameOnBothBuilds = JsonSerializer.Serialize(win10, Compact) == JsonSerializer.Serialize(win11, Compact);

        return new DocsSetting(
            s.Id,
            Text(loc, SettingLocalizationKeys.Name(s), s.Display.Name),
            Text(loc, SettingLocalizationKeys.Description(s), s.Display.Description),
            GroupName(s, loc),
            s.Control.ToString(),
            s.Display.IsSubjectivePreference,
            s.Display.AddedInVersion,
            s.UiParentId,
            Availability(s, loc),
            win11,
            sameOnBothBuilds ? null : win10);
    }

    // The builder labels option rows from the ViewModel's combo options (already localized) and only falls back to
    // the raw catalog label, which for Template_*/ServiceOption_* states is itself a key. Mirror SettingViewModelFactory.
    private static SettingStateSnapshot Snapshot(Setting s, ILocalizationService loc) => new()
    {
        Options = s.States.Select((state, i) => new ComboBoxDisplayOption(OptionLabel(s, state, i, loc), i)).ToList(),
    };

    private static string OptionLabel(Setting s, SettingState state, int i, ILocalizationService loc)
    {
        var key = SettingLocalizationKeys.IsLocalizationKey(state.Label) ? state.Label : SettingLocalizationKeys.OptionDisplay(s, i);
        return Text(loc, key, state.Label);
    }

    private static string? GroupName(Setting s, ILocalizationService loc)
    {
        var group = s.Display.GroupName;
        if (group is null) return null;
        return Text(loc, SettingLocalizationKeys.GroupCompact(group), Text(loc, SettingLocalizationKeys.GroupSnake(group), group));
    }

    private static DocsAvailability Availability(Setting s, ILocalizationService loc) =>
        new(
            s.Availability.Builds.Select(r => new DocsBuildRange(Format(r.Min), Format(r.Max))).ToList(),
            s.Availability.Hardware.Select(h => h.ToString()).ToList(),
            s.Availability.RequiresAdvancedUnlock,
            new DocsCompatibility(Compatibility(s, loc, Win10), Compatibility(s, loc, Win11)));

    private static string Format(WinBuild b) => b.Build == int.MaxValue ? "*" : $"{b.Build}.{b.Revision}";

    // Same decoding SettingsLoadingService applies in the app: "Compatibility_Key|arg|..." -> localized sentence.
    private static string? Compatibility(Setting s, ILocalizationService loc, WinBuild build)
    {
        var raw = AvailabilityCompatibility.DeriveCompatibilityMessage(s.Availability, build);
        if (raw is null) return null;

        var parts = raw.Split('|');
        var format = loc.GetString(parts[0]);
        return parts.Length > 1
            ? string.Format(CultureInfo.InvariantCulture, format, parts.Skip(1).ToArray<object>())
            : format;
    }

    private static string Text(ILocalizationService loc, string key, string fallback) =>
        loc.TryGetString(key, out var value) ? value : fallback;

    private static JsonSerializerOptions Options(bool indented) => new()
    {
        WriteIndented = indented,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        // The default encoder escapes every non-ASCII char, which is what keeps the file ASCII-only and diff-stable.
        Encoder = JavaScriptEncoder.Default,
    };
}
