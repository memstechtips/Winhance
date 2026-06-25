using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>Runs the new catalog detection engine over the same settings the old discovery just read, compares
/// each, and appends the result to a log a human reviews. Enabled only when the WINHANCE_CATALOG_SHADOW
/// environment variable is set (1/true); otherwise a no-op. Observe-only and fully exception-isolated: it never
/// changes the old result and never throws into its caller.</summary>
public sealed class DetectionShadowRunner : IDetectionShadowRunner
{
    private const string EnableVariable = "WINHANCE_CATALOG_SHADOW";
    private readonly ICatalogDetectionService _detection;
    private readonly ILogService _log;

    public DetectionShadowRunner(ICatalogDetectionService detection, ILogService log)
    {
        _detection = detection;
        _log = log;
    }

    public async Task RunAsync(
        IReadOnlyList<SettingDefinition> oldDefinitions,
        IReadOnlyDictionary<string, SettingStateResult> oldStates)
    {
        try
        {
            if (!IsEnabled())
                return;

            var oldIds = new HashSet<string>(oldDefinitions.Select(d => d.Id));
            var newById = SettingCatalog.All
                .Where(s => oldIds.Contains(s.Id))
                .ToDictionary(s => s.Id, s => s);

            var newResults = await _detection.DetectAsync(newById.Values.ToList()).ConfigureAwait(false);

            var rows = new List<ShadowRow>();
            foreach (var def in oldDefinitions)
            {
                // The old path couldn't read it (e.g. an absent scheduled task) - nothing to compare.
                if (!oldStates.TryGetValue(def.Id, out var oldResult) || oldResult is null || !oldResult.Success)
                    continue;

                newById.TryGetValue(def.Id, out var newSetting);
                newResults.TryGetValue(def.Id, out var newResult);
                rows.Add(DetectionShadowComparer.Compare(def, oldResult, newSetting, newResult));
            }

            WriteLog(rows);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warning, $"[DetectionShadowRunner] shadow run failed (ignored): {ex.Message}", ex);
        }
    }

    private static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnableVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteLog(IReadOnlyList<ShadowRow> rows)
    {
        int matches = rows.Count(r => r.Verdict == ShadowVerdict.Match);
        int diffs = rows.Count(r => r.Verdict == ShadowVerdict.Diff);
        int unpaired = rows.Count(r => r.Verdict == ShadowVerdict.Unpaired);
        int skipped = rows.Count(r => r.Verdict == ShadowVerdict.Skipped);

        var sb = new StringBuilder();
        sb.AppendLine(
            $"=== shadow run {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {rows.Count} compared, {matches} match, {diffs} diff, {unpaired} unpaired, {skipped} skipped ===");
        foreach (var r in rows)
        {
            switch (r.Verdict)
            {
                case ShadowVerdict.Match:
                    sb.AppendLine($"[MATCH] {r.Id}");
                    break;
                case ShadowVerdict.Diff:
                    sb.AppendLine($"[DIFF] {r.Id}: old={r.OldState} new={r.NewState}");
                    break;
                case ShadowVerdict.Unpaired:
                    sb.AppendLine($"[UNPAIRED] {r.Id} (no new-catalog setting)");
                    break;
                case ShadowVerdict.Skipped:
                    sb.AppendLine($"[SKIP] {r.Id}");
                    break;
            }
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winhance");
        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir, "catalog-detection-shadow.log"), sb.ToString());
    }
}
