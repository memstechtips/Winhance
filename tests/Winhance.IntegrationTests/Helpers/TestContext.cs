using Winhance.TestSupport;

namespace Winhance.IntegrationTests.Helpers;

public static class TestContext
{
    /// <summary>The solution root. See <see cref="RepoPaths"/> for why it is resolved from the
    /// caller's compile-time path rather than the bin folder.</summary>
    public static string SolutionDir => RepoPaths.SolutionDir();
}
