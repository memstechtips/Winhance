using Winhance.Core.Features.Common.Localization;

namespace Winhance.TestSupport;

// A LocKey for a string that is deliberately not in en.json. The escape hatch is internal to Core and visible
// only to this assembly, so production code cannot reach it.
public static class TestKeys
{
    public static LocKey Of(string value) => LocKey.Unchecked(value);
}
