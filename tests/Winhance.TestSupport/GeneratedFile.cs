using System.Text;

namespace Winhance.TestSupport;

/// <summary>
/// Writes a generator-owned file that lives in the repo working tree.
///
/// These files sit on the SMB share the Windows gate builds from, and a plain
/// File.WriteAllText there has repeatedly left a TRUNCATED file at a buffer boundary (0 bytes,
/// 8192, 262144) that no test wrote and that the generator cannot recover from, because it reads
/// the existing file to carry data forward. Two guards remove the exposure:
///
/// 1. Unchanged content is not written at all, so a run that changes nothing cannot corrupt
///    anything - which is almost every run.
/// 2. A changed payload goes to a temp file in the same directory and is RENAMED over the target,
///    so a partial file never exists at the real path.
/// </summary>
public static class GeneratedFile
{
    /// <summary>True if the file was rewritten, false if it already matched.</summary>
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
