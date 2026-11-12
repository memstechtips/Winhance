using System.Security.AccessControl;
using System.Security.Principal;
using Windows.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class FileSystemService(ILogService log) : IFileSystemService
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => File.ReadAllTextAsync(path, ct);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public void AppendAllText(string path, string contents, System.Text.Encoding encoding) =>
        File.AppendAllText(path, contents, encoding);
    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default) => File.WriteAllTextAsync(path, contents, ct);
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default) => File.ReadAllBytesAsync(path, ct);
    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken ct = default) => File.WriteAllBytesAsync(path, bytes, ct);
    public void DeleteFile(string path) => File.Delete(path);

    // Two writes: the owner change goes through the take-ownership privilege, and only the new owner may then
    // rewrite the DACL. One SetAccessControl would ask for both rights on the same open and be refused.
    public void TakeOwnership(string path)
    {
        ProcessPrivilege.Enable(PInvoke.SE_TAKE_OWNERSHIP_NAME);
        var file = new FileInfo(path);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        var security = file.GetAccessControl();
        security.SetOwner(administrators);
        file.SetAccessControl(security);

        security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));
        file.SetAccessControl(security);
    }

    public void CopyFile(string source, string destination, bool overwrite = false) => File.Copy(source, destination, overwrite);
    public void MoveFile(string source, string destination) => File.Move(source, destination);
    public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
    public void ExtractZip(string zipPath, string folder) => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, folder, overwriteFiles: true);
    public void CreateSymbolicLink(string link, string target) => File.CreateSymbolicLink(link, target);
    public string ReadAllText(string path, System.Text.Encoding encoding) => File.ReadAllText(path, encoding);
    public long GetFileSize(string path) => new FileInfo(path).Length;
    public void SetFileAttributes(string path, FileAttributes attributes) => new FileInfo(path).Attributes = attributes;
    public FileAttributes GetFileAttributes(string path) => File.GetAttributes(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool CreatePrivateDirectory(string path) => FolderLock.EnsurePrivateFolder(path, log);
    public void DeleteDirectory(string path, bool recursive = false) => Directory.Delete(path, recursive);
    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetFiles(path, searchPattern, searchOption);
    public string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetDirectories(path, searchPattern, searchOption);

    public string GetTempPath() => Path.GetTempPath();
    public string CombinePath(params string[] paths) => Path.Combine(paths);
    public string GetFileName(string path) => Path.GetFileName(path);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    public string GetExtension(string path) => Path.GetExtension(path);
    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
    public string GetPathRoot(string path) => Path.GetPathRoot(path)!;

    public (string Root, long FreeBytes)[] GetFixedNtfsDrives()
    {
        var drives = new List<(string Root, long FreeBytes)>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            // A drive can disappear or refuse access between GetDrives and the property reads.
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady && drive.DriveFormat == "NTFS")
                    drives.Add((drive.RootDirectory.FullName, drive.AvailableFreeSpace));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return [.. drives];
    }

    public long? GetAvailableFreeSpace(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(path)!).AvailableFreeSpace; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
}
