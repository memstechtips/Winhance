using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Turns a registry target's raw reads into the single comparable value the detection engine matches
/// against. Mirror paths fold HKLM-first to the first non-null read; REG_BINARY targets reduce to a bool
/// (bitmask) or a single byte. Pure - the raw read is injected so this is testable without a registry.
/// </summary>
public static class RegTargetReader
{
    /// <summary>
    /// Reads <paramref name="target"/> using <paramref name="rawGet"/> (keyPath, valueName) -> raw value or
    /// null when absent. Returns the reduced value and whether it is present (false = the target is absent).
    /// </summary>
    public static (object? Value, bool Present) Read(RegTarget target, Func<string, string?, object?> rawGet)
    {
        object? raw = null;
        foreach (var path in OrderHklmFirst(target.Paths))
        {
            var v = rawGet(path, target.ValueName);
            if (v != null)
            {
                raw = v;
                break; // first non-null mirror wins
            }
        }

        if (raw is null)
            return (null, false);

        // CompositeStringKey: the value is a ";"-delimited "key=value" string; reduce to the sub-value.
        if (target.CompositeStringKey is { } compositeKey)
        {
            if (raw is not string composite)
                return (null, false);
            foreach (var entry in composite.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = entry.IndexOf('=');
                if (eq > 0 && string.Equals(entry[..eq], compositeKey, System.StringComparison.OrdinalIgnoreCase))
                    return (entry[(eq + 1)..], true);
            }
            return (null, false); // sub-key not present -> absent
        }

        // REG_BINARY bit test -> bool
        if (target.BitMask is { } mask && target.ByteIndex is { } maskIdx && raw is byte[] maskBlob)
        {
            if (maskBlob.Length > maskIdx)
                return ((maskBlob[maskIdx] & mask) == mask, true);
            return (null, false);
        }

        // REG_BINARY single byte
        if (target.ByteOnly && target.ByteIndex is { } byteIdx && raw is byte[] byteBlob)
        {
            if (byteBlob.Length > byteIdx)
                return (byteBlob[byteIdx], true);
            return (null, false);
        }

        // PerNetworkInterface / PerMonitor are not handled here yet.
        return (raw, true);
    }

    private static IEnumerable<string> OrderHklmFirst(IReadOnlyList<string> paths)
        => paths.OrderByDescending(p => p.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase));
}
