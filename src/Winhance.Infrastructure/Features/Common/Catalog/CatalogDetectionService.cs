using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

// Each setting's detection is isolated in a try/catch so one failure cannot abort the batch.
internal sealed class CatalogDetectionService : ICatalogDetectionService
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
                    var detection = CatalogDiscovery.Detect(setting, context);

                    // A malformed value is a real, reportable fault on the machine and the single most useful
                    // line in a bug report about a setting "showing the wrong thing" - log it every time, with
                    // the expected and actual registry types.
                    if (detection.Outcome == SettingDetectionOutcome.Malformed)
                    {
                        _log.Log(
                            LogLevel.Warning,
                            $"[CatalogDetectionService] '{setting.Id}' has a malformed value: {detection.Detail}");
                    }

                    results[setting.Id] = new CatalogDetectionResult
                    {
                        StateLabel = detection.Label,
                        Detected = detection.Label is not null,
                        Outcome = detection.Outcome,
                        OutcomeDetail = detection.Detail,
                        AcValue = acValue,
                        DcValue = dcValue,
                        Readings = readings,
                    };
                }
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warning, $"[CatalogDetectionService] Detection failed for '{setting.Id}': {ex.Message}", ex);

                // Undetermined, NOT Custom. We do not know this setting's value, so the UI must not offer to
                // apply a state over it - that would write blind over data we failed to read.
                results[setting.Id] = new CatalogDetectionResult
                {
                    Detected = false,
                    Outcome = SettingDetectionOutcome.Undetermined,
                    OutcomeDetail = ex.Message,
                };
            }
        }

        return results;
    }

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

            // A mirror RegTarget folds its Paths into this flat list. Order HKLM-first and keep the first
            // non-null reading.
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

    // CompositeStringKey / per-NIC are intentionally NOT reduced - the raw value is stored.
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

    // Both from the context's pre-fetched cache (no extra I/O); (null, null) for every registry/task/custom-detector setting.
    private static (int? ac, int? dc) ReadPowerAcDc(Setting setting, IDetectionContext context)
    {
        foreach (var target in setting.Targets)
        {
            if (target is not PowerCfgTarget power)
                continue;
            if (power.AppliesTo.Count > 0 && !power.AppliesTo.Any(r => r.Contains(context.CurrentBuild)))
                continue;
            return (
                context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, PowerContext.AC),
                context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, PowerContext.DC));
        }
        return (null, null);
    }
}
