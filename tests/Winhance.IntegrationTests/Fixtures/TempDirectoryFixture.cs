namespace Winhance.IntegrationTests.Fixtures;

/// <summary>
/// xUnit fixture that creates a unique temporary directory for test isolation
/// and cleans it up on dispose.
/// </summary>
public class TempDirectoryFixture : IDisposable
{
    public string TempPath { get; }

    public TempDirectoryFixture()
    {
        TempPath = Path.Combine(Path.GetTempPath(), "WinhanceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(TempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(TempPath))
        {
            try
            {
                Directory.Delete(TempPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup — temp directory may be locked
            }
        }
    }
}
