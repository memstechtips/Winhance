namespace Winhance.Core.Features.Common.Catalog;

// Numeric-lenient because int/long/byte/bool box inconsistently across registry reads and config import;
// structural for REG_BINARY byte arrays; ordinal-string fallback.
internal static class CatalogValueComparer
{
    public static bool AreEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        // REG_BINARY values compare by content, not reference (and never via the ToString fallback below,
        // which would make every byte[] equal to every other).
        if (a is byte[] ba && b is byte[] bb) return ba.SequenceEqual(bb);
        if (Equals(a, b)) return true;
        try
        {
            return Convert.ToInt64(a) == Convert.ToInt64(b);   // int vs long vs byte vs bool
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
