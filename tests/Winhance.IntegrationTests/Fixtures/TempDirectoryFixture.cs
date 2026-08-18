namespace Winhance.IntegrationTests.Fixtures;

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
        GC.SuppressFinalize(this);
    }
}
