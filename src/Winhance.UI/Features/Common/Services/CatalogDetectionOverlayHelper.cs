using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Overlays the new catalog detection engine's authoritative state onto old-discovery results: it populates the typed
/// AcValue/DcValue/DynamicOptions/DynamicSelection fields and overwrites RawValues["ACValue"]/["DCValue"] while
/// preserving every other RawValues entry. Shared by SettingsLoadingService (the live UI load/refresh) and the export
/// services (ConfigExportService, AutounattendXmlGeneratorService) so there is ONE overlay implementation - the single
/// source of truth for catalog detection state, instead of each exporter reading the old discovery engine directly.
/// </summary>
internal static class CatalogDetectionOverlayHelper
{
    /// <summary>
    /// Overlays the new catalog detection engine's authoritative primary state (a toggle's on/off, a selection's
    /// chosen option index) onto <paramref name="batchStates"/> for every setting that has a catalog peer, threading
    /// the typed AC/DC + dynamic-option fields. The old result's other auxiliary data (RawValues, TooltipData) is
    /// preserved, unpaired settings keep their old state, and any failure is logged and leaves the old states in place
    /// so detection never hard-fails the caller.
    /// </summary>
    public static async Task OverlayAsync(
        IReadOnlyList<SettingDefinition> definitions,
        Dictionary<string, SettingStateResult> batchStates,
        ICatalogDetectionService catalogDetectionService,
        ILogService logService)
    {
        try
        {
            var ids = new HashSet<string>(definitions.Select(d => d.Id));
            var pairedSettings = SettingCatalog.All.Where(s => ids.Contains(s.Id)).ToList();
            if (pairedSettings.Count == 0)
                return;

            var newResults = await catalogDetectionService.DetectAsync(pairedSettings);

            foreach (var def in definitions)
            {
                if (!batchStates.TryGetValue(def.Id, out var oldState))
                    continue;
                newResults.TryGetValue(def.Id, out var newResult);
                batchStates[def.Id] = CatalogDetectionStateOverlay.Apply(def, oldState, newResult);
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning,
                $"[CatalogDetectionOverlayHelper] Catalog detection overlay failed (keeping old states): {ex.Message}");
        }
    }
}
