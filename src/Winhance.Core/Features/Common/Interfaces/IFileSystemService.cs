using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Abstracts file system operations for testability.
/// Wraps System.IO.File, System.IO.Directory, and System.IO.Path static calls.
/// </summary>
public interface IFileSystemService
{
    // File operations
    bool FileExists(string path);
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    void WriteAllText(string path, string contents);
    Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct = default);
    void DeleteFile(string path);
    void CopyFile(string source, string destination, bool overwrite = false);
    void MoveFile(string source, string destination);
    string ReadAllText(string path, System.Text.Encoding encoding);
    long GetFileSize(string path);
    void SetFileAttributes(string path, System.IO.FileAttributes attributes);
    DateTime GetLastWriteTime(string path);

    // Directory operations
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive = false);
    string[] GetFiles(string path, string searchPattern = "*", System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly);
    string[] GetDirectories(string path, string searchPattern = "*", System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly);

    // Path operations
    string GetTempPath();
    string CombinePath(params string[] paths);
    string GetFileName(string path);
    string? GetDirectoryName(string path);
    string GetExtension(string path);
    string GetFileNameWithoutExtension(string path);
    string GetPathRoot(string path);
}
