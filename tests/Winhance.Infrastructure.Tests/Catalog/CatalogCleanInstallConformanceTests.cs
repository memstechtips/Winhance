using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// Conformance: on a CLEAN Windows install, the catalog's detection must resolve every setting to its
/// WindowsDefault state. Replays the REAL detection pipeline (<see cref="CatalogDiscovery.Detect"/> over
/// <see cref="RegTargetReader"/> / <see cref="StateDetectionEngine"/>) against three committed clean-install
/// probe captures (tests/.../Catalog/Fixtures/cleaninstall-*.json), hydrating <see cref="IDetectionContext"/>
/// from the probe readings. This is the C# successor to the audit's Python reconciler
/// (extras/probe/reconcile-defaults.py) - same replay, but through the production code itself, so a
/// WindowsDefault role or accepted-value regression fails here without a Windows machine.
///
/// Fixtures (see the 2026-07-2x windows-defaults audit docs for provenance):
///  - cleaninstall-win10-22h2-pro-vm.json: 19045.2965 Pro VM, express OOBE, at image patch level, en-ZA.
///  - cleaninstall-win11-25h2-gold-laptop.json: 26200.8037 Home SL laptop, PRE-UPDATE, privacy-DECLINED,
///    en-US - the audit's gold oracle. Only this fixture carries powercfg data (Balanced shipped defaults;
///    a clean install's SCHEME_CURRENT equals them).
///  - cleaninstall-win11-25h2-post-update-vm.json: 26200.8246 Pro VM, express OOBE, ~2 CUs past the image,
///    en-ZA. Carries documented post-update drift; its EXPECTED set is correspondingly larger.
///
/// Each fixture pins its EXPECTED divergence set exactly (SetEquals): a NEW divergence fails, and a pinned
/// divergence that silently resolves fails too (the pin must then be removed consciously). Every pinned id
/// carries its audit-doc reason. Scope mirrors what the app would surface on that machine: settings hidden by
/// Availability (build gating, absent tasks, absent powercfg) are excluded, as are custom-detector settings,
/// Actions, sliders (no state labels; their defaults are pinned by DefaultConfigConformanceTests), the dynamic
/// power-plan selection, and settings whose targets the fixture predates (catalog drift past the probe).
///
/// Run: winhance-harness CatalogCleanInstallConformanceTests
/// </summary>
public class CatalogCleanInstallConformanceTests
{
    private readonly ITestOutputHelper _output;

    public CatalogCleanInstallConformanceTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------------------------------------
    // Expected divergences, per fixture. Reasons cite the audit findings (docs/2026-07-2*-windows-
    // defaults-*.md). "@AC"/"@DC" suffixes mark powercfg per-context comparisons.
    // ---------------------------------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, string> Win10VmExpected = new Dictionary<string, string>
    {
        ["gaming-memory-integrity"] = "hardware-conditional: VBS values absent on incapable/VM hardware; capable hardware writes Enabled=1 (held, no OrAbsent)",
        ["gaming-virtualization-based-security"] = "hardware-conditional: same as memory-integrity",
    };

    private static readonly IReadOnlyDictionary<string, string> GoldLaptopExpected = new Dictionary<string, string>
    {
        ["gaming-memory-integrity"] = "hardware-conditional: this laptop has VBS off",
        ["gaming-virtualization-based-security"] = "hardware-conditional: this laptop has VBS off",
        ["privacy-advertising-id"] = "privacy band: WindowsDefault is the EXPRESS state (decision 11b.2); this fixture is privacy-DECLINED, diverging by design",
        ["privacy-improve-inking-typing"] = "privacy band: declined fixture vs express default (by design)",
        ["privacy-tailored-experiences"] = "privacy band: declined fixture vs express default (by design)",
        ["privacy-turn-off-copilot"] = "privacy band: declined fixture leaves Copilot policies unwritten vs express default Disabled (by design)",
    };

    private static readonly IReadOnlyDictionary<string, string> PostUpdateVmExpected = new Dictionary<string, string>
    {
        ["gaming-memory-integrity"] = "hardware-conditional: VM, VBS off",
        ["gaming-virtualization-based-security"] = "hardware-conditional: VM, VBS off",
        ["notifications-system-pane-suggestions"] = "post-image drift documented in the image-comparison doc (1 -> 0 after updates); image + gold conform",
        ["power-fast-startup"] = "VM artifact: no hibernation support on VMware, HiberbootEnabled=0; image + real hardware ship 1",
        ["security-bitlocker-auto-encryption"] = "VM artifact: PreventDeviceEncryption=1 is an unsupported-hardware write; image ships nothing, gold conforms",
        ["security-smart-app-control"] = "SAC self-disabled post-update (documented 2 -> 0 drift); image + gold ship Evaluation",
        ["start-disable-bing-search-results"] = "this express VM carries the Copilot-policy BingSearchEnabled=0 write; the declined gold conforms to Enabled",
        ["taskbar-search-box-11"] = "post-update VM state contradicts image + gold (documented drift/tweak on this capture)",
        ["taskbar-widgets"] = "post-update VM state contradicts image + gold (documented drift/tweak on this capture)",
    };

    [Fact]
    public void Win10_22H2_vm_clean_install_detects_windows_defaults()
        => RunFixture("cleaninstall-win10-22h2-pro-vm.json", Win10VmExpected);

    [Fact]
    public void Win11_25H2_gold_laptop_clean_install_detects_windows_defaults()
        => RunFixture("cleaninstall-win11-25h2-gold-laptop.json", GoldLaptopExpected);

    [Fact]
    public void Win11_25H2_post_update_vm_clean_install_detects_windows_defaults()
        => RunFixture("cleaninstall-win11-25h2-post-update-vm.json", PostUpdateVmExpected);

    // ---------------------------------------------------------------------------------------------

    private void RunFixture(string fixtureName, IReadOnlyDictionary<string, string> expectedDivergent)
    {
        var fixture = ProbeFixture.Load(FixturePath(fixtureName));
        var context = new ProbeDetectionContext(fixture);
        var build = fixture.Build;

        var divergent = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var skips = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int compared = 0;
        var comparedIds = new HashSet<string>(StringComparer.Ordinal);
        void Skip(string reason) => skips[reason] = skips.GetValueOrDefault(reason) + 1;

        foreach (var setting in SettingCatalog.All)
        {
            if (setting.Detector is not null) { Skip("custom-detector"); continue; }

            switch (setting.Control)
            {
                case ControlKind.Action: Skip("action"); continue;
                case ControlKind.PowerPlan: Skip("dynamic-power-plan"); continue;
                case ControlKind.Slider: Skip("numeric-slider"); continue;
            }

            if (!setting.Availability.Allows(build)) { Skip("build-unavailable"); continue; }

            var activeTargets = setting.Targets
                .Where(t => t.AppliesTo.Count == 0 || t.AppliesTo.Any(r => r.Contains(build)))
                .ToList();
            if (activeTargets.Count == 0) { Skip("no-active-targets"); continue; }

            // Fixture coverage: every active registry target must have per-path readings; every active task
            // target a captured row; every active powercfg target a Present shipped-defaults entry.
            bool covered = true, existenceHidden = false, powerUncaptured = false;
            foreach (var target in activeTargets)
            {
                if (target is RegTarget reg)
                {
                    if (!fixture.CoversRegTarget(setting.Id, reg)) { covered = false; break; }
                }
                else if (target is TaskTarget task)
                {
                    if (!fixture.Tasks.TryGetValue(task.TaskPath, out var enabled)) { covered = false; break; }
                    if (enabled is null) { existenceHidden = true; break; } // task absent on this machine -> card hidden
                }
                else if (target is PowerCfgTarget power)
                {
                    if (!fixture.HasPowerCfgData) { powerUncaptured = true; break; }
                    if (!fixture.PowerCfg.ContainsKey(PowerKey(power.SubgroupGuid, power.SettingGuid)))
                    { existenceHidden = true; break; } // setting not shipped on this machine -> card hidden
                }
            }
            if (powerUncaptured) { Skip("powercfg-not-captured"); continue; }
            if (existenceHidden) { Skip("existence-hidden"); continue; }
            if (!covered) { Skip("not-in-probe"); continue; }

            bool isPowerCfg = activeTargets.OfType<PowerCfgTarget>().Any();
            var contexts = isPowerCfg
                ? new[] { PowerContext.AC, PowerContext.DC }
                : new[] { PowerContext.Always };

            foreach (var pc in contexts)
            {
                var roleContext = pc == PowerContext.Always ? PowerContext.Always : pc;
                var defaults = setting.States
                    .Where(s => s.HasRole(RoleKind.WindowsDefault, build, roleContext))
                    .ToList();
                if (defaults.Count == 0) { Skip("no-windows-default"); continue; }
                Assert.True(defaults.Count == 1,
                    $"'{setting.Id}' resolves {defaults.Count} WindowsDefault states for build {build.Build} ({roleContext}) - overlapping role scopes.");

                string? detected = CatalogDiscovery.Detect(
                    setting, context, pc == PowerContext.Always ? PowerContext.AC : pc).Label;

                compared++;
                comparedIds.Add(setting.Id);
                if (!string.Equals(detected, defaults[0].Label, StringComparison.Ordinal))
                {
                    string key = pc == PowerContext.Always ? setting.Id : $"{setting.Id}@{pc}";
                    divergent[key] = $"detected={(detected ?? "<Custom>")}, default={defaults[0].Label}";
                }
            }
        }

        _output.WriteLine($"fixture {fixtureName}: compared {compared} (settings {comparedIds.Count}), " +
            "skips: " + string.Join(", ", skips.Select(kv => $"{kv.Key}={kv.Value}")));
        foreach (var kv in divergent)
            _output.WriteLine($"  divergent: {kv.Key} ({kv.Value})");

        // Non-vacuity: the replay must cover the bulk of the catalog, and the audit's sanity anchor
        // (the Recycle Bin desktop icon, shown on every clean install) must be compared and conformant.
        Assert.True(compared >= 200, $"only {compared} comparisons ran - fixture/scoping regression.");
        Assert.True(comparedIds.Contains("explorer-customization-desktop-icon-recycle-bin"),
            "recycle-bin was not compared - scoping regression.");
        Assert.False(divergent.ContainsKey("explorer-customization-desktop-icon-recycle-bin"),
            "recycle-bin diverged - the sanity anchor of the whole audit.");

        var unexpected = divergent.Keys.Where(k => !expectedDivergent.ContainsKey(k)).ToList();
        var resolved = expectedDivergent.Keys.Where(k => !divergent.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0,
            $"NEW clean-install divergences in {fixtureName} (detection no longer resolves the WindowsDefault):\n"
            + string.Join("\n", unexpected.Select(k => $"  {k}: {divergent[k]}")));
        Assert.True(resolved.Count == 0,
            $"PINNED divergences no longer occur in {fixtureName} (remove them from the expected set):\n  "
            + string.Join("\n  ", resolved));
    }

    private static string PowerKey(string subgroupGuid, string settingGuid)
        => subgroupGuid.ToLowerInvariant() + "/" + settingGuid.ToLowerInvariant();

    private static string FixturePath(string name)
        => Path.Combine(SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Catalog", "Fixtures", name);

    // Anchors on the compile-time source path (RecommendedConfigConformanceTests precedent) so fixtures
    // resolve from the repo even when the build output is redirected off the network share.
    private static string SolutionDir() => RepoPaths.SolutionDir();

    // ---------------------------------------------------------------------------------------------
    // Fixture model + detection context
    // ---------------------------------------------------------------------------------------------

    /// <summary>A parsed clean-install probe capture: per-setting per-target per-path registry readings,
    /// scheduled-task enabled flags, and (when captured) the shipped Balanced-scheme powercfg defaults.</summary>
    private sealed class ProbeFixture
    {
        // Matches CatalogProbeManifestGeneratorTests.EmptyKeySentinel (a target with Key == "" reads a key's
        // (Default) value; the sentinel stands in for the empty JSON property name).
        private const string EmptyKeySentinel = "(target:default-value)";
        private const string BalancedSchemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

        public WinBuild Build { get; private set; }
        public bool HasPowerCfgData { get; private set; }

        // (settingId, joinKey) -> path (OrdinalIgnoreCase) -> (status, value)
        private readonly Dictionary<(string, string), Dictionary<string, (string Status, object? Value)>> _registry = new();
        public Dictionary<string, bool?> Tasks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, (int? Ac, int? Dc)> PowerCfg { get; } = new(StringComparer.Ordinal);

        public static ProbeFixture Load(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var fx = new ProbeFixture();

            var machine = root.GetProperty("machine");
            fx.Build = new WinBuild(machine.GetProperty("buildNumber").GetInt32(), machine.GetProperty("ubr").GetInt32());

            foreach (var setting in root.GetProperty("settings").EnumerateArray())
            {
                string id = setting.GetProperty("id").GetString()!;
                foreach (var target in setting.GetProperty("targets").EnumerateArray())
                {
                    if (target.GetProperty("kind").GetString() != "Registry")
                        continue;
                    string joinKey = target.GetProperty("joinKey").GetString()!;
                    var perPath = new Dictionary<string, (string, object?)>(StringComparer.OrdinalIgnoreCase);
                    if (target.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in paths.EnumerateArray())
                        {
                            string keyPath = p.GetProperty("path").GetString()!;
                            string status = p.GetProperty("status").GetString() ?? "Error";
                            perPath[keyPath] = (status, ConvertValue(p.GetProperty("value")));
                        }
                    }
                    fx._registry[(id, joinKey)] = perPath;
                }
            }

            if (root.TryGetProperty("scheduledTasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tasks.EnumerateArray())
                {
                    string taskPath = t.GetProperty("taskPath").GetString()!;
                    bool? enabled = t.GetProperty("enabled").ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => (bool?)null,
                    };
                    fx.Tasks[taskPath] = enabled;
                }
            }

            if (root.TryGetProperty("powerCfgDefaults", out var pcs) && pcs.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in pcs.EnumerateArray())
                {
                    if (e.GetProperty("status").GetString() != "Present")
                        continue;
                    string key = e.GetProperty("subgroupGuid").GetString()!.ToLowerInvariant()
                        + "/" + e.GetProperty("settingGuid").GetString()!.ToLowerInvariant();
                    foreach (var scheme in e.GetProperty("schemes").EnumerateArray())
                    {
                        if (!string.Equals(scheme.GetProperty("scheme").GetString(), BalancedSchemeGuid, StringComparison.OrdinalIgnoreCase))
                            continue;
                        int? ac = scheme.TryGetProperty("ac", out var acEl) && acEl.ValueKind == JsonValueKind.Number ? acEl.GetInt32() : (int?)null;
                        int? dc = scheme.TryGetProperty("dc", out var dcEl) && dcEl.ValueKind == JsonValueKind.Number ? dcEl.GetInt32() : (int?)null;
                        fx.PowerCfg[key] = (ac, dc);
                        fx.HasPowerCfgData = true;
                    }
                }
            }

            return fx;
        }

        private static string JoinKey(string key) => key.Length == 0 ? EmptyKeySentinel : key;

        /// <summary>True when the fixture holds a per-path reading for EVERY path of the target (the catalog
        /// has not drifted past the probe for this target).</summary>
        public bool CoversRegTarget(string settingId, RegTarget reg)
        {
            if (!_registry.TryGetValue((settingId, JoinKey(reg.Key)), out var perPath))
                return false;
            return reg.Paths.All(perPath.ContainsKey);
        }

        public bool TryGetReading(string settingId, RegTarget reg, string path, out (string Status, object? Value) reading)
        {
            reading = default;
            return _registry.TryGetValue((settingId, JoinKey(reg.Key)), out var perPath)
                && perPath.TryGetValue(path, out reading);
        }

        private static object? ConvertValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number => value.TryGetInt32(out var i) ? i : value.GetInt64(),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object when value.TryGetProperty("$bytes", out var hex) =>
                Convert.FromHexString(hex.GetString() ?? string.Empty),
            _ => null,
        };
    }

    /// <summary>IDetectionContext hydrated from a probe fixture. Registry reads are served from a
    /// (path, valueName) map built by JOINING the live catalog's RegTargets with the fixture's per-path
    /// readings; key existence from KeyPresent/KeyMissing rows plus any path with a present/value-absent
    /// reading. Custom-detector members keep their interface defaults - detector settings are out of scope.</summary>
    private sealed class ProbeDetectionContext : IDetectionContext
    {
        private readonly ProbeFixture _fixture;
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _keyExists = new(StringComparer.OrdinalIgnoreCase);

        public ProbeDetectionContext(ProbeFixture fixture)
        {
            _fixture = fixture;

            // Join catalog targets to fixture readings once, keyed (path \n valueName) / path.
            foreach (var setting in SettingCatalog.All)
            {
                foreach (var reg in setting.Targets.OfType<RegTarget>())
                {
                    foreach (var path in reg.Paths)
                    {
                        if (!fixture.TryGetReading(setting.Id, reg, path, out var reading))
                            continue;

                        if (reg.ValueName is null)
                        {
                            // key-existence target: KeyPresent/KeyMissing
                            bool exists = reading.Status == "KeyPresent";
                            _keyExists[path] = _keyExists.GetValueOrDefault(path) || exists;
                            continue;
                        }

                        if (reading.Status is "Present" or "ValueAbsent")
                            _keyExists[path] = true;
                        else if (!_keyExists.ContainsKey(path))
                            _keyExists[path] = false;

                        // Two settings sharing a (path, valueName) were probed on the same machine and
                        // agree; first reading wins.
                        string mapKey = path + "\n" + reg.ValueName;
                        _values.TryAdd(mapKey, reading.Status == "Present" ? reading.Value : null);
                    }
                }
            }
        }

        public WinBuild CurrentBuild => _fixture.Build;

        public object? GetValue(string keyPath, string? valueName)
            => valueName is null ? null : _values.GetValueOrDefault(keyPath + "\n" + valueName);

        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();

        public bool KeyExists(string keyPath) => _keyExists.GetValueOrDefault(keyPath);

        public string? PrimaryDnsV4OfActiveAdapter() => null;

        public bool IsSystemRestoreEnabled() => false;

        public bool? ScheduledTaskEnabled(string taskPath) => _fixture.Tasks.GetValueOrDefault(taskPath);

        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context)
        {
            if (!_fixture.PowerCfg.TryGetValue(
                    subgroupGuid.ToLowerInvariant() + "/" + settingGuid.ToLowerInvariant(), out var values))
                return null;
            return context == PowerContext.DC ? values.Dc : values.Ac;
        }

        public string? ActivePowerPlanGuid() => null;
    }
}
