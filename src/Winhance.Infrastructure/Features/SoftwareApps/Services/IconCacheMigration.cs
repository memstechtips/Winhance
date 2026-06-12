using System;
using System.IO;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>One-time IconCache wipe gated by a .schema sentinel. Bumping
/// CurrentSchemaVersion forces every machine to purge the cache once on next launch,
/// so the icon model can change without leaving stale per-source files behind.</summary>
public class IconCacheMigration(ILogService logService)
{
    public const int CurrentSchemaVersion = 2;

    public void EnsureSchema(string cacheRoot, int currentVersion = CurrentSchemaVersion)
    {
        try
        {
            Directory.CreateDirectory(cacheRoot);
            var sentinel = Path.Combine(cacheRoot, ".schema");
            int existing = 0;
            if (File.Exists(sentinel) && int.TryParse(File.ReadAllText(sentinel).Trim(), out var v)) existing = v;
            if (existing >= currentVersion) return;

            foreach (var f in Directory.EnumerateFiles(cacheRoot))
                if (!string.Equals(Path.GetFileName(f), ".schema", StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(f); }
                    catch (Exception ex) { logService.LogWarning($"IconCacheMigration: could not delete {f}: {ex.Message}"); }

            File.WriteAllText(sentinel, currentVersion.ToString());
            logService.LogInformation($"IconCacheMigration: wiped cache, schema {existing} -> {currentVersion}");
        }
        catch (Exception ex) { logService.LogWarning($"IconCacheMigration failed: {ex.Message}"); }
    }
}
