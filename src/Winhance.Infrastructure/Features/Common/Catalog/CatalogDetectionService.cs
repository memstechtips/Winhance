using System;
using System.Collections.Generic;
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
                if (setting.Numeric is not null)
                {
                    int? value = CatalogDiscovery.DetectValue(setting, context);
                    results[setting.Id] = new CatalogDetectionResult { Value = value, Detected = value.HasValue };
                }
                else
                {
                    string? label = CatalogDiscovery.DetectState(setting, context);
                    results[setting.Id] = new CatalogDetectionResult { StateLabel = label, Detected = label is not null };
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
}
