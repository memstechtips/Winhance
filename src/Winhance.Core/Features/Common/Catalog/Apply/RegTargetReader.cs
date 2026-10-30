using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Catalog;

// Mirror paths fold HKLM-first to the first non-null read. A present value of the wrong CLR type reads as
// Malformed, never as the raw value: a wrong-format value must not surface as "Custom".
public static class RegTargetReader
{
    // A target whose ValueName is null encodes its state as key existence.
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

        // ";"-separated list after a fixed prefix -> bool. A "showonly:" allow-list is the same fact inverted: the
        // item is listed (hidden) when it is NOT in the allow-list. An empty allow-list hides every page: Settings
        // will not open at all while it is set.
        if (target.ListItem is { } listItem)
        {
            if (raw is not string list)
                return TargetReading.Malformed;
            if (ListItems(list, target.ListPrefix) is { } items)
                return TargetReading.Of(items.Contains(listItem, StringComparer.OrdinalIgnoreCase));
            if (ShowOnlyItems(list) is { } allowed)
                return TargetReading.Of(!allowed.Contains(listItem, StringComparer.OrdinalIgnoreCase));
            return TargetReading.Of(false);
        }

        // Decimal-string flags bit test -> bool (e.g. accessibility Flags "62")
        if (target.StringFlagMask is { } flagMask)
        {
            if (raw is not string flagStr)
                return TargetReading.Malformed;
            if (long.TryParse(flagStr, out var flags))
                return TargetReading.Of((flags & flagMask) == flagMask);
            return TargetReading.Absent; // right type, unparseable content - treat as absent
        }

        // REG_BINARY bit test -> bool
        if (target.BitMask is { } mask && target.ByteIndex is { } maskIdx)
        {
            if (raw is not byte[] maskBlob)
                return TargetReading.Malformed;
            if (maskBlob.Length > maskIdx)
                return TargetReading.Of((maskBlob[maskIdx] & mask) == mask);
            return TargetReading.Absent; // right type, blob too short
        }

        // REG_BINARY single byte
        if (target.ByteOnly && target.ByteIndex is { } byteIdx)
        {
            if (raw is not byte[] byteBlob)
                return TargetReading.Malformed;
            if (byteBlob.Length > byteIdx)
                return TargetReading.Of(byteBlob[byteIdx]);
            return TargetReading.Absent; // right type, blob too short
        }

        // Plain value. Deliberately NOT type-checked against RegTarget.Type: CatalogValueComparer is
        // numeric-lenient (Convert.ToInt64), so a DWord target holding REG_SZ "1" still matches Of(1)
        // and detects correctly today - flagging it would regress working settings for no gain. Such a
        // target also needs no repair path: the plain apply write already passes RegTarget.Type, and
        // RegistryKey.SetValue overwrites an existing value's type.
        // PerNetworkInterface / PerMonitor are not handled here yet.
        return TargetReading.Of(raw);
    }

    // Null when the value does not start with the prefix, so a writer can leave a list it does not own alone.
    public static List<string>? ListItems(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : null;

    public const string ShowOnlyPrefix = "showonly:";

    public static List<string>? ShowOnlyItems(string value) => ListItems(value, ShowOnlyPrefix);

    public static RegTarget Target(Setting setting, string key) => setting.Targets.OfType<RegTarget>().First(t => t.Key == key);

    public static object? Read(Setting setting, string key, IDetectionContext ctx) => Read(Target(setting, key), ctx).Value;

    // Internal so CatalogDiscovery can name the same winning path when describing a malformed value.
    internal static IEnumerable<string> OrderHklmFirst(IReadOnlyList<string> paths)
        => paths.OrderByDescending(p => p.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase));
}

// Present is false when the target is absent OR unusable, so no matcher sees a value it cannot interpret.
// KindMismatch: the value exists but its CLR type cannot satisfy the target's shape - malformed, not absent.
public readonly record struct TargetReading(object? Value, bool Present, bool KindMismatch)
{
    public static readonly TargetReading Absent = new(null, false, false);

    public static TargetReading Of(object? value) => new(value, true, false);

    // Never Present: a malformed value must not reach the matchers.
    public static readonly TargetReading Malformed = new(null, false, true);

    public void Deconstruct(out object? value, out bool present)
    {
        value = Value;
        present = Present;
    }
}
