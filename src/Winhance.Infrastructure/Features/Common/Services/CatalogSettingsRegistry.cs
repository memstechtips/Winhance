using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// Holds the machine CONTEXT (build, hardware caps, existence-passed id set) resolved once in InitializeAsync,
// and answers membership as a PURE QUERY over (catalog x context x scope) - no mutable filter flag.
internal sealed class CatalogSettingsRegistry : ICatalogSettingsRegistry
{
    private readonly IWindowsVersionService _version;
    private readonly IHardwareDetectionService _hardware;
    private readonly ICatalogPowerExistenceFilter _existence;

    private WinBuild _build;
    private HardwareCaps _caps;
    private HashSet<string> _existencePassed = new();
    private Dictionary<string, string> _featureById = new();
    private bool _initialized;

    public CatalogSettingsRegistry(IWindowsVersionService version, IHardwareDetectionService hardware, ICatalogPowerExistenceFilter existence)
    {
        _version = version;
        _hardware = hardware;
        _existence = existence;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _build = new WinBuild(_version.GetWindowsBuildNumber(), _version.GetWindowsBuildRevision());
        // The cold call that pays for the WMI round trip, so it is offloaded here rather than inside the
        // service. Unknown battery hides battery-only settings: offering one that cannot work is worse.
        _caps = await Task.Run(() => new HardwareCaps(
            _hardware.HasBattery() ?? false,
            _hardware.SupportsHybridSleep())).ConfigureAwait(false);

        // Resolve powercfg existence ONCE over the OS-version-INDEPENDENT candidate set (hardware-passing settings
        // that validate existence). Existence is machine-state (GUID presence), orthogonal to the OS-build gate, so
        // one resolution serves BOTH scopes. FilterAsync keeps a setting that passed (or does not require) existence;
        // cache the surviving ids.
        var candidates = SettingCatalog.All
            .Where(s => CatalogMembershipFilter.IsAvailableIgnoringOsBuild(s, _caps) && s.Availability.ValidatesExistence)
            .ToList();
        _existencePassed = candidates.Count > 0
            ? (await _existence.FilterAsync(candidates).ConfigureAwait(false)).Select(s => s.Id).ToHashSet()
            : new HashSet<string>();

        // The owning feature is scope-independent (a setting belongs to exactly one feature regardless of the gate).
        var featureById = new Dictionary<string, string>();
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
            foreach (var s in settings)
                featureById[s.Id] = featureId;
        _featureById = featureById;

        _initialized = true;
    }

    private bool IsMember(Setting s, bool includeOtherOsVersions)
    {
        var osHwOk = includeOtherOsVersions
            ? CatalogMembershipFilter.IsAvailableIgnoringOsBuild(s, _caps)
            : CatalogMembershipFilter.IsAvailable(s, _build, _caps);
        if (!osHwOk) return false;
        return !s.Availability.ValidatesExistence || _existencePassed.Contains(s.Id);
    }

    // Without this an uninitialized registry answers over a default (0,0) build + empty existence set, silently
    // hiding every build-gated / powercfg setting - which surfaces downstream as a misleading "Setting not found"
    // on apply. Every live consumer queries post-startup; this converts a swallowed-init failure into a loud, accurate error.
    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("CatalogSettingsRegistry not initialized. Call InitializeAsync first.");
    }

    public IReadOnlyList<Setting> GetByFeature(string featureId, bool includeOtherOsVersions = false)
    {
        EnsureInitialized();
        return SettingCatalog.ByFeature.TryGetValue(featureId, out var settings)
            ? settings.Where(s => IsMember(s, includeOtherOsVersions)).ToList()
            : Array.Empty<Setting>();
    }

    public Setting? GetById(string settingId, bool includeOtherOsVersions = false)
    {
        EnsureInitialized();
        return SettingCatalog.ById.TryGetValue(SettingIdAliases.Normalize(settingId), out var s) && IsMember(s, includeOtherOsVersions)
            ? s
            : null;
    }

    public string? GetFeatureIdForSetting(string settingId)
    {
        EnsureInitialized();
        return _featureById.TryGetValue(SettingIdAliases.Normalize(settingId), out var f) ? f : null;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll(bool includeOtherOsVersions = false)
    {
        EnsureInitialized();
        var result = new Dictionary<string, IReadOnlyList<Setting>>();
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
            result[featureId] = settings.Where(s => IsMember(s, includeOtherOsVersions)).ToList();
        return result;
    }
}
