namespace Winhance.IntegrationTests.Helpers;

public static class TestContext
{
    /// <summary>
    /// Resolves the solution root directory by walking up from the test assembly location.
    /// </summary>
    public static string SolutionDir
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Winhance.sln")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }

            return dir ?? throw new InvalidOperationException(
                "Could not find Winhance.sln in any parent directory of " + AppContext.BaseDirectory);
        }
    }
}
