using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// GENERATOR (not an assertion test): dumps the live <see cref="SettingCatalog"/> to JSON and splices that JSON
/// into <c>extras/probe/Probe-WinhanceDefaults.template.ps1</c>, producing the single self-contained
/// <c>extras/probe/Probe-WinhanceDefaults.ps1</c> that Marco drops on a clean VM.
///
/// WHY IT LIVES IN A TEST PROJECT: the catalog is C# and a PowerShell script cannot read it, so the manifest has to
/// be produced by real C# iterating <see cref="SettingCatalog.All"/>. Regex-parsing the catalog source is NOT an
/// acceptable substitute - it silently misses shape (build-scoped roles, mirror paths, binary reductions) that this
/// walk gets right by construction. The test project is the only thing on the Windows worker that already compiles
/// against Winhance.Core, so this rides in as a [Fact] and runs via <c>winhance-harness CatalogProbeManifest</c>.
///
/// It writes into the repo working tree (via the <see cref="SolutionDir"/> CallerFilePath anchor, the same trick
/// RecommendedConfigConformanceTests uses so it resolves the repo even when the build output is redirected off a
/// network share). Both outputs are committed - the previous one-shot config generator was written, used and
/// deleted, and regenerating it cost real time.
///
/// Run: winhance-harness CatalogProbeManifest      (or: dotnet test --filter CatalogProbeManifest)
/// </summary>
public class CatalogProbeManifestGeneratorTests
{
    private const string ManifestPlaceholder = "@@MANIFEST_JSON@@";
    private const int ManifestSchemaVersion = 1;

    /// <summary>
    /// Four catalog targets declare <c>Key == ""</c> (they read a key's (Default) value, so there is no value name
    /// to key off). A target Key is only ever a JOIN HANDLE between a target and the state <c>Set</c> entries that
    /// reference it - it is never a registry name - so an empty one is substituted for this sentinel wherever it is
    /// used as a JSON property name. Empty-string property names are at best untested through PS 5.1's
    /// <c>ConvertFrom-Json</c> / <c>PSObject.Properties</c>, and a silently dropped property would make those
    /// settings vanish from the probe's finding with no error. The faithful Key is still emitted as <c>key</c>.
    /// </summary>
    private const string EmptyKeySentinel = "(target:default-value)";

    private static string JoinKey(string key) => key.Length == 0 ? EmptyKeySentinel : key;

    private readonly ITestOutputHelper _output;

    public CatalogProbeManifestGeneratorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Generate_probe_script_and_catalog_manifest()
    {
        var settings = SettingCatalog.All;
        Assert.True(settings.Count > 300, $"only {settings.Count} settings enumerated - catalog composition bug.");

        var featureBySettingId = SettingCatalog.ByFeature
            .SelectMany(kvp => kvp.Value.Select(s => (s.Id, Feature: kvp.Key)))
            .ToDictionary(x => x.Id, x => x.Feature, StringComparer.Ordinal);

        // Join keys must be unique within a setting - they are what ties a state's Set entry to a target. The
        // empty-key sentinel could in principle collide with a literal key of the same text; assert it does not.
        foreach (var s in settings)
        {
            var joinKeys = s.Targets.Select(t => JoinKey(t.Key)).ToList();
            Assert.True(
                joinKeys.Distinct(StringComparer.Ordinal).Count() == joinKeys.Count,
                $"setting '{s.Id}' has duplicate target join keys: {string.Join(", ", joinKeys)}");
        }

        // Identifies WHICH catalog revision a returned probe .json was produced against, without a timestamp
        // (which would make every regeneration a spurious diff). Stable across regenerations of unchanged data.
        string catalogHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(BuildSettingsArrayJson(settings, featureBySettingId)))).ToLowerInvariant();

        // The embedded copy is compact + single-line so it can live in a PowerShell here-string; the committed
        // standalone copy is indented so it diffs readably and can be read by the step-2 reconciliation.
        string compact = BuildManifestJson(settings, featureBySettingId, catalogHash, indented: false);
        string readable = BuildManifestJson(settings, featureBySettingId, catalogHash, indented: true);
        _output.WriteLine($"catalogHash       : {catalogHash}");

        // --- Guarantees the PowerShell here-string depends on -------------------------------------------------
        // The script embeds this in @'...'@. A line consisting of "'@" would terminate it early, and PS 5.1
        // mis-parses some non-ASCII. Utf8JsonWriter's default encoder escapes both apostrophes and non-ASCII,
        // so these assertions should hold by construction - they are here to fail loudly if that ever changes.
        Assert.DoesNotContain('\n', compact);
        Assert.DoesNotContain('\r', compact);
        Assert.DoesNotContain('\'', compact);
        Assert.True(compact.All(c => c < 128), "manifest JSON contains non-ASCII - it cannot be embedded safely.");

        // Round-trips as valid JSON.
        using (var doc = JsonDocument.Parse(compact))
        {
            Assert.Equal(settings.Count, doc.RootElement.GetProperty("settings").GetArrayLength());
        }

        var probeDir = Path.Combine(SolutionDir(), "extras", "probe");
        Directory.CreateDirectory(probeDir);

        var templatePath = Path.Combine(probeDir, "Probe-WinhanceDefaults.template.ps1");
        Assert.True(File.Exists(templatePath), $"probe template not found at {templatePath}");
        var template = File.ReadAllText(templatePath);
        Assert.True(
            template.Contains(ManifestPlaceholder, StringComparison.Ordinal),
            $"template is missing the {ManifestPlaceholder} placeholder.");

        var script = template.Replace(ManifestPlaceholder, compact, StringComparison.Ordinal);
        Assert.True(script.All(c => c < 128), "generated probe script contains non-ASCII - PS 5.1 may mis-parse it.");

        var scriptPath = Path.Combine(probeDir, "Probe-WinhanceDefaults.ps1");
        var manifestPath = Path.Combine(probeDir, "catalog-probe-manifest.json");
        File.WriteAllText(scriptPath, Crlf(script), new UTF8Encoding(false));
        File.WriteAllText(manifestPath, Crlf(readable), new UTF8Encoding(false));

        var regTargets = settings.SelectMany(s => s.Targets).OfType<RegTarget>().Count();
        var powerTargets = settings.SelectMany(s => s.Targets).OfType<PowerCfgTarget>().Count();
        var taskTargets = settings.SelectMany(s => s.Targets).OfType<TaskTarget>().Count();

        _output.WriteLine($"settings          : {settings.Count}");
        _output.WriteLine($"registry targets  : {regTargets}");
        _output.WriteLine($"powercfg targets  : {powerTargets} (recorded, not probed)");
        _output.WriteLine($"task targets      : {taskTargets}");
        _output.WriteLine($"manifest (compact): {compact.Length:N0} chars");
        _output.WriteLine($"wrote             : {scriptPath}");
        _output.WriteLine($"wrote             : {manifestPath}");

        Assert.True(regTargets > 300, $"only {regTargets} registry targets - dump is not walking Targets correctly.");
    }

    // Normalise to CRLF: the repo is CRLF throughout and the generator may run from either OS.
    private static string Crlf(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");

    private static string BuildManifestJson(
        IReadOnlyList<Setting> settings,
        IReadOnlyDictionary<string, string> featureBySettingId,
        string catalogHash,
        bool indented)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            w.WriteStartObject();
            w.WriteNumber("schemaVersion", ManifestSchemaVersion);
            // No timestamp on purpose: it would make every regeneration a diff even when the catalog is
            // unchanged. catalogHash identifies the revision and is stable across regenerations.
            w.WriteString("catalogHash", catalogHash);
            w.WriteNumber("settingCount", settings.Count);

            w.WriteStartArray("settings");
            foreach (var setting in settings)
                WriteSetting(w, setting, featureBySettingId);
            w.WriteEndArray();

            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // The settings array alone, compact - the input to catalogHash. Kept separate so the hash covers the catalog
    // content only and is not perturbed by the envelope (which contains the hash itself).
    private static string BuildSettingsArrayJson(
        IReadOnlyList<Setting> settings, IReadOnlyDictionary<string, string> featureBySettingId)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartArray();
            foreach (var setting in settings) WriteSetting(w, setting, featureBySettingId);
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSetting(
        Utf8JsonWriter w, Setting setting, IReadOnlyDictionary<string, string> featureBySettingId)
    {
        w.WriteStartObject();
        w.WriteString("id", setting.Id);
        w.WriteString("feature", featureBySettingId.TryGetValue(setting.Id, out var f) ? f : "");
        w.WriteString("control", setting.Control.ToString());

        // Power settings carry AC/DC contexts, and a WindowsDefault role is scoped per context - so a
        // context-scoped setting has more than one Windows default and cannot be reduced to a single one.
        w.WriteStartArray("contexts");
        foreach (var c in setting.Contexts) w.WriteStringValue(c.ToString());
        w.WriteEndArray();

        // A setting with a custom detector does NOT resolve by matching States.Set, so the step-2 reconciliation
        // must exclude it from any naive "does the reading match the WindowsDefault state" comparison.
        if (setting.Detector is null) w.WriteNull("detector");
        else w.WriteString("detector", setting.Detector.GetType().Name);

        if (setting.OptionSource is null) w.WriteNull("optionSource");
        else w.WriteString("optionSource", setting.OptionSource.GetType().Name);

        w.WriteStartObject("availability");
        WriteBuildRanges(w, "builds", setting.Availability.Builds);
        w.WriteStartArray("hardware");
        foreach (var h in setting.Availability.Hardware) w.WriteStringValue(h.ToString());
        w.WriteEndArray();
        w.WriteBoolean("validatesExistence", setting.Availability.ValidatesExistence);
        w.WriteBoolean("requiresAdvancedUnlock", setting.Availability.RequiresAdvancedUnlock);
        w.WriteEndObject();

        if (setting.Numeric is null)
        {
            w.WriteNull("numeric");
        }
        else
        {
            w.WriteStartObject("numeric");
            w.WriteNumber("min", setting.Numeric.Min);
            w.WriteNumber("max", setting.Numeric.Max);
            if (setting.Numeric.Units is null) w.WriteNull("units"); else w.WriteString("units", setting.Numeric.Units);
            WriteContextValues(w, "recommended", setting.Numeric.Recommended);
            WriteContextValues(w, "windowsDefault", setting.Numeric.WindowsDefault);
            w.WriteEndObject();
        }

        w.WriteStartArray("targets");
        foreach (var target in setting.Targets) WriteTarget(w, target);
        w.WriteEndArray();

        w.WriteStartArray("states");
        foreach (var state in setting.States) WriteState(w, state);
        w.WriteEndArray();

        w.WriteEndObject();
    }

    private static void WriteTarget(Utf8JsonWriter w, Target target)
    {
        w.WriteStartObject();
        w.WriteString("key", target.Key);
        w.WriteString("joinKey", JoinKey(target.Key));
        WriteBuildRanges(w, "appliesTo", target.AppliesTo);

        switch (target)
        {
            case RegTarget reg:
                w.WriteString("kind", "Registry");
                w.WriteStartArray("paths");
                foreach (var p in reg.Paths) w.WriteStringValue(p);
                w.WriteEndArray();
                if (reg.ValueName is null) w.WriteNull("valueName"); else w.WriteString("valueName", reg.ValueName);
                // The distinction a probe MUST NOT lose: ValueName == null means the state IS key existence
                // (RegTargetReader branches on `is null`), whereas ValueName == "" is the key's (Default) VALUE and
                // must be read like any other. Four catalog targets use "". PowerShell cannot tell them apart after
                // a [string] cast - [string]$null is "" - so the distinction is emitted as its own boolean rather
                // than left to be re-derived downstream.
                w.WriteBoolean("keyExistenceOnly", reg.ValueName is null);
                w.WriteString("type", reg.Type.ToString());
                w.WriteBoolean("isGroupPolicy", reg.IsGroupPolicy);
                // ApplyOnly excludes a target from the PRECEDENCE path's deciding-target selection
                // (CatalogDiscovery.regReadTargets) only. CatalogDiscovery still populates a reading for it, and on
                // a whole-pattern setting StateDetectionEngine evaluates its Set entry like any other - so this is
                // NOT simply "not read on detect". Today all ApplyOnly targets sit on precedence-shaped settings.
                w.WriteBoolean("applyOnly", reg.ApplyOnly);
                if (reg.LockWhenValue is null) w.WriteNull("lockWhenValue");
                else w.WriteNumber("lockWhenValue", reg.LockWhenValue.Value);
                if (reg.ByteIndex is null) w.WriteNull("byteIndex"); else w.WriteNumber("byteIndex", reg.ByteIndex.Value);
                if (reg.BitMask is null) w.WriteNull("bitMask"); else w.WriteNumber("bitMask", reg.BitMask.Value);
                if (reg.StringFlagMask is null) w.WriteNull("stringFlagMask"); else w.WriteNumber("stringFlagMask", reg.StringFlagMask.Value);
                w.WriteNumber("stringFlagAbsentBase", reg.StringFlagAbsentBase);
                w.WriteBoolean("byteOnly", reg.ByteOnly);
                if (reg.CompositeStringKey is null) w.WriteNull("compositeStringKey");
                else w.WriteString("compositeStringKey", reg.CompositeStringKey);
                w.WriteBoolean("perNetworkInterface", reg.PerNetworkInterface);
                w.WriteBoolean("perMonitor", reg.PerMonitor);
                break;

            case PowerCfgTarget power:
                w.WriteString("kind", "PowerCfg");
                w.WriteString("subgroupGuid", power.SubgroupGuid);
                w.WriteString("settingGuid", power.SettingGuid);
                w.WriteString("mode", power.Mode.ToString());
                if (power.Units is null) w.WriteNull("units"); else w.WriteString("units", power.Units);
                w.WriteBoolean("checkForHardwareControl", power.CheckForHardwareControl);
                // Emitted in full, not as a bool: this is the key whose Attributes = 0 WRITE would be needed to
                // unhide the setting before querying it - the reason v1 skips powercfg. A v2 that wants to read
                // (not write) the enablement key needs its paths.
                if (power.EnablementKey is null)
                {
                    w.WriteNull("enablementKey");
                }
                else
                {
                    w.WriteStartObject("enablementKey");
                    w.WriteString("key", power.EnablementKey.Key);
                    w.WriteStartArray("paths");
                    foreach (var p in power.EnablementKey.Paths) w.WriteStringValue(p);
                    w.WriteEndArray();
                    if (power.EnablementKey.ValueName is null) w.WriteNull("valueName");
                    else w.WriteString("valueName", power.EnablementKey.ValueName);
                    w.WriteString("type", power.EnablementKey.Type.ToString());
                    w.WriteEndObject();
                }
                break;

            case TaskTarget task:
                w.WriteString("kind", "Task");
                w.WriteString("taskPath", task.TaskPath);
                break;

            default:
                w.WriteString("kind", target.GetType().Name);
                break;
        }

        w.WriteEndObject();
    }

    private static void WriteState(Utf8JsonWriter w, SettingState state)
    {
        w.WriteStartObject();
        w.WriteString("label", state.Label);
        w.WriteBoolean("isFallback", state.IsFallback);

        w.WriteStartArray("roles");
        foreach (var role in state.Roles)
        {
            w.WriteStartObject();
            w.WriteString("kind", role.Kind.ToString());
            w.WriteString("context", role.Context.ToString());
            // Empty appliesTo = unconditional. A NON-empty one is a build-scoped role, which the build-unaware
            // HasRole overload deliberately ignores - the probe must therefore evaluate it against the live build.
            WriteBuildRanges(w, "appliesTo", role.AppliesTo);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        // Keyed by joinKey (see EmptyKeySentinel) and sorted: IReadOnlyDictionary order is insertion order in
        // practice but is not contractual, and sorting makes a regeneration of unchanged data diff-stable by
        // construction. Key order is semantically irrelevant - Matches is evaluated per key.
        w.WriteStartObject("set");
        foreach (var kvp in state.Set.OrderBy(k => JoinKey(k.Key), StringComparer.Ordinal))
            WriteStateValue(w, JoinKey(kvp.Key), kvp.Value);
        w.WriteEndObject();

        if (state.ResetSet is null)
        {
            w.WriteNull("resetSet");
        }
        else
        {
            w.WriteStartObject("resetSet");
            foreach (var kvp in state.ResetSet.OrderBy(k => JoinKey(k.Key), StringComparer.Ordinal))
                WriteStateValue(w, JoinKey(kvp.Key), kvp.Value);
            w.WriteEndObject();
        }

        w.WriteEndObject();
    }

    private static void WriteStateValue(Utf8JsonWriter w, string targetKey, StateValue value)
    {
        w.WriteStartObject(targetKey);
        // acceptsAbsent IS the .OrAbsent() flag - the single field the whole audit turns on.
        w.WriteBoolean("acceptsAbsent", value.AcceptsAbsent);
        w.WriteBoolean("acceptsAnyPresent", value.AcceptsAnyPresent);
        w.WriteBoolean("deleteOnWrite", value.DeleteOnWrite);
        w.WriteStartArray("values");
        foreach (var v in value.AcceptedValues) WriteCatalogValue(w, v);
        w.WriteEndArray();
        w.WritePropertyName("writePayload");
        WriteCatalogValue(w, value.WritePayload);
        w.WriteEndObject();
    }

    private static void WriteCatalogValue(Utf8JsonWriter w, object? value)
    {
        switch (value)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case byte[] bytes:
                w.WriteStartObject();
                w.WriteString("$bytes", Convert.ToHexString(bytes));
                w.WriteNumber("length", bytes.Length);
                w.WriteEndObject();
                break;
            case string s: w.WriteStringValue(s); break;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                w.WriteNumberValue(Convert.ToInt64(value));
                break;
            default:
                // Nothing in the catalog should land here; recorded with its CLR type rather than silently
                // stringified so a new value shape is visible instead of quietly losing fidelity.
                w.WriteStartObject();
                w.WriteString("$clrType", value.GetType().FullName ?? "unknown");
                w.WriteString("$value", value.ToString() ?? "");
                w.WriteEndObject();
                break;
        }
    }

    private static void WriteContextValues(Utf8JsonWriter w, string name, IReadOnlyList<ContextValue> values)
    {
        w.WriteStartArray(name);
        foreach (var cv in values)
        {
            w.WriteStartObject();
            w.WriteString("context", cv.Context.ToString());
            w.WriteNumber("value", cv.Value);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void WriteBuildRanges(Utf8JsonWriter w, string name, IReadOnlyList<BuildRange> ranges)
    {
        w.WriteStartArray(name);
        foreach (var r in ranges)
        {
            w.WriteStartObject();
            w.WriteNumber("minBuild", r.Min.Build);
            w.WriteNumber("minRevision", r.Min.Revision);
            w.WriteNumber("maxBuild", r.Max.Build);
            w.WriteNumber("maxRevision", r.Max.Revision);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    // Anchors on the compile-time source path (same as RecommendedConfigConformanceTests) so the repo resolves
    // even when the build output lives outside the tree on a redirected/network-share build root.
    private static string SolutionDir([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath);
        while (dir != null && !File.Exists(Path.Combine(dir, "Winhance.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not find Winhance.sln walking up from " + callerPath);
    }
}
