using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>Composes the proven membership pieces into a catalog-sourced registry: per feature, filter
/// SettingCatalog.ByFeature by CatalogMembershipFilter (OS build + hardware caps), then apply
/// CatalogPowerExistenceFilter where any setting validates existence (powercfg). Additive - no consumer yet;
/// the old CompatibleSettingsRegistry stays authoritative until the coordinated consumer cutover.</summary>
public sealed class CatalogSettingsRegistry : ICatalogSettingsRegistry
{
    private readonly IWindowsVersionService _version;
    private readonly IHardwareDetectionService _hardware;
    private readonly ICatalogPowerExistenceFilter _existence;
    private Dictionary<string, IReadOnlyList<Setting>> _byFeature = new();
    private Dictionary<string, Setting> _byId = new();
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

        var build = new WinBuild(_version.GetWindowsBuildNumber(), _version.GetWindowsBuildRevision());
        var caps = new HardwareCaps(
            await _hardware.HasBatteryAsync().ConfigureAwait(false),
            await _hardware.HasLidAsync().ConfigureAwait(false),
            await _hardware.SupportsBrightnessControlAsync().ConfigureAwait(false),
            await _hardware.SupportsHybridSleepAsync().ConfigureAwait(false));

        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();
        var byId = new Dictionary<string, Setting>();
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            var osHw = settings.Where(s => CatalogMembershipFilter.IsAvailable(s, build, caps)).ToList();
            IReadOnlyList<Setting> filtered = osHw.Any(s => s.Availability.ValidatesExistence)
                ? await _existence.FilterAsync(osHw).ConfigureAwait(false)
                : osHw;
            byFeature[featureId] = filtered;
            foreach (var s in filtered) byId[s.Id] = s;
        }

        _byFeature = byFeature;
        _byId = byId;
        _initialized = true;
    }

    public IReadOnlyList<Setting> GetByFeature(string featureId) =>
        _byFeature.TryGetValue(featureId, out var s) ? s : Array.Empty<Setting>();

    public Setting? GetById(string settingId) =>
        _byId.TryGetValue(SettingIdAliases.Normalize(settingId), out var s) ? s : null;

    public IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll() => _byFeature;
}
