using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Default implementation that delegates to System.IO static methods.
/// </summary>
public class FileSystemService : IFileSystemService
{
    // File operations
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => File.ReadAllTextAsync(path, ct);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default) => File.WriteAllTextAsync(path, contents, ct);
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default) => File.ReadAllBytesAsync(path, ct);
    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct = default) => File.WriteAllBytesAsync(path, bytes, ct);
    public void DeleteFile(string path) => File.Delete(path);
    public void CopyFile(string source, string destination, bool overwrite = false) => File.Copy(source, destination, overwrite);
    public void MoveFile(string source, string destination) => File.Move(source, destination);
    public string ReadAllText(string path, System.Text.Encoding encoding) => File.ReadAllText(path, encoding);
    public long GetFileSize(string path) => new FileInfo(path).Length;
    public void SetFileAttributes(string path, FileAttributes attributes) => new FileInfo(path).Attributes = attributes;
    public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);

    // Directory operations
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void DeleteDirectory(string path, bool recursive = false) => Directory.Delete(path, recursive);
    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetFiles(path, searchPattern, searchOption);
    public string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetDirectories(path, searchPattern, searchOption);

    // Path operations
    public string GetTempPath() => Path.GetTempPath();
    public string CombinePath(params string[] paths) => Path.Combine(paths);
    public string GetFileName(string path) => Path.GetFileName(path);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    public string GetExtension(string path) => Path.GetExtension(path);
    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
    public string GetPathRoot(string path) => Path.GetPathRoot(path)!;
}
