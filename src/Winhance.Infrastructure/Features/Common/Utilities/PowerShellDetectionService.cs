using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Utilities;

public sealed class PowerShellDetectionService(ILogService logService) : IPowerShellDetectionService
{
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyDictionary<string, bool>> DetectAsync(
        IEnumerable<SettingDefinition> settings,
        CancellationToken ct = default)
    {
        var jobs = settings
            .Select(s => (
                Id: s.Id,
                Script: s.PowerShellScripts?
                    .Select(p => p.DetectionScript)
                    .FirstOrDefault(script => !string.IsNullOrWhiteSpace(script))))
            .Where(j => !string.IsNullOrWhiteSpace(j.Script))
            .ToList();

        var results = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (jobs.Count == 0) return results;

        var sw = Stopwatch.StartNew();
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        foreach (var (id, script) in jobs)
        {
            if (ct.IsCancellationRequested)
            {
                logService.Log(LogLevel.Info,
                    $"[PowerShellDetectionService] Cancellation requested; '{id}' and any remaining settings recorded as Disabled");
                results[id] = false;
                continue;
            }

            if (sw.Elapsed >= BatchTimeout)
            {
                logService.Log(LogLevel.Warning,
                    $"[PowerShellDetectionService] Batch timeout exceeded ({BatchTimeout.TotalSeconds}s); '{id}' and any remaining settings recorded as Disabled");
                results[id] = false;
                continue;
            }

            try
            {
                using var ps = PowerShell.Create();
                ps.Runspace = runspace;
                ps.AddScript(script);

                var remaining = BatchTimeout - sw.Elapsed;
                var invokeTask = Task.Run(() => ps.Invoke(), ct);
                var completed = await Task.WhenAny(invokeTask, Task.Delay(remaining, ct)).ConfigureAwait(false);

                if (completed != invokeTask)
                {
                    try { ps.Stop(); } catch { /* best effort */ }
                    try { await invokeTask.ConfigureAwait(false); }
                    catch { /* expected after Stop — PipelineStoppedException, OperationCanceledException, etc. */ }
                    logService.Log(LogLevel.Warning,
                        $"[PowerShellDetectionService] Detection script for '{id}' timed out within batch budget; recording as Disabled");
                    results[id] = false;
                    continue;
                }

                var output = await invokeTask.ConfigureAwait(false);
                results[id] = InterpretOutput(output, id);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning,
                    $"[PowerShellDetectionService] Detection script for '{id}' threw {ex.GetType().Name}: {ex.Message}");
                results[id] = false;
            }
        }

        return results;
    }

    private bool InterpretOutput(System.Collections.ObjectModel.Collection<PSObject> output, string settingId)
    {
        if (output is null || output.Count == 0)
        {
            logService.Log(LogLevel.Warning,
                $"[PowerShellDetectionService] Detection script for '{settingId}' produced no output; recording as Disabled");
            return false;
        }

        // Walk last-to-first so the last meaningful value wins.
        for (int i = output.Count - 1; i >= 0; i--)
        {
            var obj = output[i]?.BaseObject;
            if (obj is null) continue;

            switch (obj)
            {
                case bool b:
                    return b;
                case int n:
                    return n != 0;
                case long n:
                    return n != 0;
                case short n:
                    return n != 0;
                case byte n:
                    return n != 0;
                case sbyte n:
                    return n != 0;
                case uint n:
                    return n != 0;
                case ulong n:
                    return n != 0;
                case ushort n:
                    return n != 0;
                default:
                    continue; // Skip strings, hashtables, anything else — not a valid output.
            }
        }

        logService.Log(LogLevel.Warning,
            $"[PowerShellDetectionService] Detection script for '{settingId}' returned no boolean-or-numeric value (contract: emit $true/$false or 1/0); recording as Disabled");
        return false;
    }
}
