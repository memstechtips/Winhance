using System.Text;

namespace Winhance.TestSupport;

// These files sit on the SMB share the Windows gate builds from, and a plain File.WriteAllText there has
// repeatedly left a TRUNCATED file at a buffer boundary (0, 8192, 262144 bytes) that the generator cannot
// recover from (it reads the existing file to carry data forward). Unchanged content is not written at all; a
// changed payload goes to a temp file in the same directory and is RENAMED over the target.
public static class GeneratedFile
{
    public static bool WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return false;

        // Same directory, so the rename is a metadata operation on one volume rather than a copy.
        var staging = path + ".generating";
        try
        {
            File.WriteAllText(staging, text, new UTF8Encoding(false));
            File.Move(staging, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }

        return true;
    }
}
