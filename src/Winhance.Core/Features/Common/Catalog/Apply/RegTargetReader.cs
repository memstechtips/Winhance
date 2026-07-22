using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Turns a registry target's reads into the single comparable value the detection engine matches
/// against. Mirror paths fold HKLM-first to the first non-null read; REG_BINARY targets reduce to a bool
/// (bitmask) or a single byte. Reads go through the injected <see cref="IDetectionContext"/> so this is
/// testable without a real registry.
/// </summary>
public static class RegTargetReader
{
    /// <summary>
    /// Reads <paramref name="target"/> through <paramref name="ctx"/>. A target whose ValueName is null
    /// encodes its state as key existence, so its reading is (null, key-exists); otherwise the raw value is
    /// read and reduced. Returns the reduced value and whether it is present (false = the target is absent).
    /// </summary>
    public static (object? Value, bool Present) Read(RegTarget target, IDetectionContext ctx)
    {
        // ValueName == null: the state is whether the key exists, not a stored value. Mirror paths fold
        // HKLM-first - the first existing key wins; absent under every path means not present.
        if (target.ValueName is null)
        {
            foreach (var path in OrderHklmFirst(target.Paths))
            {
                if (ctx.KeyExists(path))
                    return (null, true);
            }
            return (null, false);
        }

        object? raw = null;
        foreach (var path in OrderHklmFirst(target.Paths))
        {
            var v = ctx.GetValue(path, target.ValueName);
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

        // Decimal-string flags bit test -> bool (e.g. accessibility Flags "62")
        if (target.StringFlagMask is { } flagMask && raw is string flagStr)
        {
            if (long.TryParse(flagStr, out var flags))
                return ((flags & flagMask) == flagMask, true);
            return (null, false);
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
