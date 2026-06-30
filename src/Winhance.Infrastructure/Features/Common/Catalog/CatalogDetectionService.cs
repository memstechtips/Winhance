using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>Drives <see cref="CatalogDiscovery"/> over a batch of settings using a fresh, pre-fetched detection
/// context. Each setting's detection is isolated in a try/catch so one failure cannot abort the batch. The result
/// is the new engine's normalized view (state label or numeric value) keyed by Setting.Id; mapping it into the
/// UI's SettingStateResult is a later cutover step.</summary>
public sealed class CatalogDetectionService : ICatalogDetectionService
{
    private readonly ISystemDetectionContextFactory _contextFactory;
    private readonly ILogService _log;

    public CatalogDetectionService(ISystemDetectionContextFactory contextFactory, ILogService log)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    public async Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings)
    {
        var results = new Dictionary<string, CatalogDetectionResult>();

        var context = _contextFactory.Create();
        await context.PrefetchAsync(settings).ConfigureAwait(false);

        foreach (var setting in settings)
        {
            try
            {
                if (setting.OptionSource is { } optionSource)
                {
                    // Runtime-sourced options (e.g. the installed power plans): the source enumerates the live
                    // options and reports the current selection (a Value such as the active scheme GUID) directly
                    // from the pre-fetched context - no static states, no separate current-selection detector. The
                    // StateLabel carries that Value so the UI resolves the chosen option by value (no index round-trip).
                    var options = optionSource.EnumerateOptions(context);
                    string? current = optionSource.CurrentSelection(context);
                    string? currentName = optionSource.CurrentSelectionName(context);
                    results[setting.Id] = new CatalogDetectionResult
                    {
                        StateLabel = current,
                        Detected = current is not null,
                        Options = options,
                        DynamicSelectionName = currentName,
                    };
                    continue;
                }

                var readings = BuildReadings(setting, context);
                var (acValue, dcValue) = ReadPowerAcDc(setting, context);
                if (setting.Numeric is not null)
                {
                    int? value = CatalogDiscovery.DetectValue(setting, context);
                    results[setting.Id] = new CatalogDetectionResult { Value = value, Detected = value.HasValue, AcValue = acValue, DcValue = dcValue, Readings = readings };
                }
                else
                {
                    string? label = CatalogDiscovery.DetectState(setting, context);
                    results[setting.Id] = new CatalogDetectionResult { StateLabel = label, Detected = label is not null, AcValue = acValue, DcValue = dcValue, Readings = readings };
                }
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warning, $"[CatalogDetectionService] Detection failed for '{setting.Id}': {ex.Message}", ex);
                results[setting.Id] = new CatalogDetectionResult { Detected = false };
            }
        }

        return results;
    }

    /// <summary>Builds the live per-registry-target readings for <paramref name="setting"/>, reproducing the old
    /// discovery's RawValues exactly (SystemSettingsDiscoveryService): group the RegTargets by
    /// <c>ValueName ?? "KeyExists"</c>, read each group's paths HKLM-first and keep the first non-null reading
    /// (REG_BINARY reduced via <see cref="Reduce"/>, key existence as a bool), so the config-export custom-state path
    /// reads identical values off the new engine. Null when the setting has no registry targets.</summary>
    private static IReadOnlyDictionary<string, object?>? BuildReadings(Setting setting, IDetectionContext context)
    {
        var regTargets = setting.Targets.OfType<RegTarget>().ToList();
        if (regTargets.Count == 0)
            return null;

        var readings = new Dictionary<string, object?>();
        foreach (var group in regTargets.GroupBy(rt => rt.ValueName ?? "KeyExists"))
        {
            object? finalValue = null;
            bool found = false;

            // One old RegistrySetting == one (target, path); a mirror RegTarget folds its Paths into this flat list.
            // Order HKLM-first and keep the first non-null reading, matching the old discovery's per-group fold.
            var reads = group
                .SelectMany(rt => rt.Paths.Select(path => (Target: rt, Path: path)))
                .OrderByDescending(x => x.Path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase));

            foreach (var (target, path) in reads)
            {
                object? value = target.ValueName is null
                    ? context.KeyExists(path)
                    : Reduce(target, context.GetValue(path, target.ValueName));

                if (value != null || !found)
                {
                    finalValue = value;
                    found = true;
                    if (value != null)
                        break;
                }
            }

            readings[group.Key] = finalValue;
        }

        return readings;
    }

    /// <summary>Applies the same REG_BINARY reduction the old discovery did when building RawValues: a bitmask test
    /// reduces to a bool, a single-byte edit to that byte (null when the blob is too short); everything else passes
    /// through. CompositeStringKey / per-NIC are intentionally NOT reduced - the old RawValues stored the raw value.</summary>
    private static object? Reduce(RegTarget target, object? raw)
    {
        if (raw is byte[] blob)
        {
            if (target.BitMask is { } mask && target.ByteIndex is { } bitIdx)
                return blob.Length > bitIdx ? (object?)((blob[bitIdx] & mask) == mask) : null;
            if (target.ByteOnly && target.ByteIndex is { } byteIdx)
                return blob.Length > byteIdx ? (object?)blob[byteIdx] : null;
        }
        return raw;
    }

    /// <summary>Reads the raw AC and DC powercfg values for a setting's live <see cref="PowerCfgTarget"/> (the first
    /// whose AppliesTo admits the current build, mirroring <see cref="CatalogDiscovery"/>'s target filter). Both come
    /// from the context's already pre-fetched cache (no extra I/O). (null, null) when the setting has no live powercfg
    /// target - i.e. for every registry/task/custom-detector setting.</summary>
    private static (int? ac, int? dc) ReadPowerAcDc(Setting setting, IDetectionContext context)
    {
        foreach (var target in setting.Targets)
        {
            if (target is not PowerCfgTarget power)
                continue;
            if (power.AppliesTo.Count > 0 && !power.AppliesTo.Any(r => r.Contains(context.CurrentBuild)))
                continue; // target not live on this build
            return (
                context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, PowerContext.AC),
                context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, PowerContext.DC));
        }
        return (null, null);
    }
}
