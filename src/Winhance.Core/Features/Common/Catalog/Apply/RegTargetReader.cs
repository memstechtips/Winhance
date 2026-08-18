namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Turns a registry target's reads into the single comparable value the detection engine matches
/// against. Mirror paths fold HKLM-first to the first non-null read; REG_BINARY targets reduce to a bool
/// (bitmask) or a single byte. Reads go through the injected <see cref="IDetectionContext"/> so this is
/// testable without a real registry.
///
/// A "surgical" target (bitmask, single byte, decimal-string flags, packed composite string) needs a
/// specific CLR type to reduce at all. When the stored value is present but of the wrong type, the
/// reading is <see cref="TargetReading.Malformed"/> rather than the raw value: previously such a value
/// fell through and was returned marked PRESENT, where it could match no state and so surfaced as
/// "Custom" - reporting a wrong-format value as an unrecognized choice.
/// </summary>
public static class RegTargetReader
{
    /// <summary>
    /// Reads <paramref name="target"/> through <paramref name="ctx"/>. A target whose ValueName is null
    /// encodes its state as key existence, so its reading is (null, key-exists); otherwise the raw value is
    /// read and reduced.
    /// </summary>
    public static TargetReading Read(RegTarget target, IDetectionContext ctx)
    {
        // ValueName == null: the state is whether the key exists, not a stored value. Mirror paths fold
        // HKLM-first - the first existing key wins; absent under every path means not present.
        if (target.ValueName is null)
        {
            foreach (var path in OrderHklmFirst(target.Paths))
            {
                if (ctx.KeyExists(path))
                    return TargetReading.Of(null);
            }
            return TargetReading.Absent;
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
            return TargetReading.Absent;

        // CompositeStringKey: the value is a ";"-delimited "key=value" string; reduce to the sub-value.
        if (target.CompositeStringKey is { } compositeKey)
        {
            if (raw is not string composite)
                return TargetReading.Malformed; // packed sub-keys can only live in a string
            foreach (var entry in composite.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = entry.IndexOf('=');
                if (eq > 0 && string.Equals(entry[..eq], compositeKey, StringComparison.OrdinalIgnoreCase))
                    return TargetReading.Of(entry[(eq + 1)..]);
            }
            return TargetReading.Absent; // sub-key not present -> absent (the string itself is fine)
        }

        // Decimal-string flags bit test -> bool (e.g. accessibility Flags "62")
        if (target.StringFlagMask is { } flagMask)
        {
            if (raw is not string flagStr)
                return TargetReading.Malformed;
            if (long.TryParse(flagStr, out var flags))
                return TargetReading.Of((flags & flagMask) == flagMask);
            return TargetReading.Absent; // right type, unparseable content - treat as absent, as before
        }

        // REG_BINARY bit test -> bool
        if (target.BitMask is { } mask && target.ByteIndex is { } maskIdx)
        {
            if (raw is not byte[] maskBlob)
                return TargetReading.Malformed;
            if (maskBlob.Length > maskIdx)
                return TargetReading.Of((maskBlob[maskIdx] & mask) == mask);
            return TargetReading.Absent; // right type, blob too short - as before
        }

        // REG_BINARY single byte
        if (target.ByteOnly && target.ByteIndex is { } byteIdx)
        {
            if (raw is not byte[] byteBlob)
                return TargetReading.Malformed;
            if (byteBlob.Length > byteIdx)
                return TargetReading.Of(byteBlob[byteIdx]);
            return TargetReading.Absent; // right type, blob too short - as before
        }

        // Plain value. Deliberately NOT type-checked against RegTarget.Type: CatalogValueComparer is
        // numeric-lenient (Convert.ToInt64), so a DWord target holding REG_SZ "1" still matches Of(1)
        // and detects correctly today - flagging it would regress working settings for no gain. Such a
        // target also needs no repair path: the plain apply write already passes RegTarget.Type, and
        // RegistryKey.SetValue overwrites an existing value's type.
        // PerNetworkInterface / PerMonitor are not handled here yet.
        return TargetReading.Of(raw);
    }

    /// <summary>Mirror-path read order: an HKLM path outranks HKCU. Internal so
    /// <see cref="CatalogDiscovery"/> can name the same winning path when describing a malformed value.</summary>
    internal static IEnumerable<string> OrderHklmFirst(IReadOnlyList<string> paths)
        => paths.OrderByDescending(p => p.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase));
}
