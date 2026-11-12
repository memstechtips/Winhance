namespace Winhance.Core.Features.Common.Interfaces;

public interface IFileSystemService
{
    bool FileExists(string path);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    void WriteAllText(string path, string contents);
    void AppendAllText(string path, string contents, System.Text.Encoding encoding);
    Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct = default);
    void DeleteFile(string path);
    // Makes Administrators the owner with full control, for a file only TrustedInstaller may change.
    void TakeOwnership(string path);
    void CopyFile(string source, string destination, bool overwrite = false);
    void MoveFile(string source, string destination);
    void MoveDirectory(string source, string destination);
    void ExtractZip(string zipPath, string folder);
    void CreateSymbolicLink(string link, string target);
    string ReadAllText(string path, System.Text.Encoding encoding);
    long GetFileSize(string path);
    void SetFileAttributes(string path, System.IO.FileAttributes attributes);
    System.IO.FileAttributes GetFileAttributes(string path);

    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    // Creates the folder writable only by SYSTEM and Administrators, replacing a junction or a user-owned one; false when it cannot.
    bool CreatePrivateDirectory(string path);
    void DeleteDirectory(string path, bool recursive = false);
    string[] GetFiles(string path, string searchPattern = "*", System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly);
    string[] GetDirectories(string path, string searchPattern = "*", System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly);

    string GetTempPath();
    string CombinePath(params string[] paths);
    string GetFileName(string path);
    string? GetDirectoryName(string path);
    string GetExtension(string path);
    string GetFileNameWithoutExtension(string path);
    string GetPathRoot(string path);

    (string Root, long FreeBytes)[] GetFixedNtfsDrives();
    // Null when the path has no local drive behind it (a network share) or the drive cannot be read.
    long? GetAvailableFreeSpace(string path);
}
