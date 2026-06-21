using System;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Core-local value equality for detection matching. Numeric-lenient (int/long/byte/bool box
/// inconsistently across registry reads and config import), structural for REG_BINARY byte arrays, with an
/// ordinal-string fallback. Lives in Core deliberately - the new model must not depend on Winhance.Infrastructure.</summary>
internal static class CatalogValueComparer
{
    public static bool AreEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        // REG_BINARY values compare by content, not reference (and never via the ToString fallback below,
        // which would make every byte[] equal to every other). Mirrors the old ValueComparer.
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
