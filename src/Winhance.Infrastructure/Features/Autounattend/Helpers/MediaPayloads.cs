using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Autounattend.Helpers;

internal sealed class MediaRollbackException(string folder, Exception error)
    : IOException($"Could not restore the previous media bundle. Recovery files are in {folder}.", error) { }

internal static class MediaPayloads
{
    private const string WallpaperRoot = PictureFileConstants.CarriedPictureFolder + @"\";
    private const string LegacyWallpaperRoot = PictureFileConstants.LegacyCarriedPictureFolder + @"\";
    private const string ScriptRoot = @"C:\ProgramData\Winhance\Unattend\Scripts\";
    private static readonly XNamespace X = AutounattendDocument.WinhanceExtensions;

    internal static IEnumerable<XElement> List(XElement extensions) => extensions.Elements(X + "StagedFile");

    internal static string? MediaPath(string mediaFolder, string destination)
    {
        string prefix;
        string root;
        if (destination.StartsWith(WallpaperRoot, StringComparison.OrdinalIgnoreCase))
        {
            prefix = WallpaperRoot;
            root = Path.Combine(mediaFolder, "sources", "$OEM$", "$1", "Wallpapers", "Winhance");
        }
        else if (destination.StartsWith(LegacyWallpaperRoot, StringComparison.OrdinalIgnoreCase))
        {
            prefix = LegacyWallpaperRoot;
            root = Path.Combine(mediaFolder, "sources", "$OEM$", "$$", "Web", "Wallpaper", "Winhance");
        }
        else if (destination.StartsWith(ScriptRoot, StringComparison.OrdinalIgnoreCase))
        {
            prefix = ScriptRoot;
            root = Path.Combine(mediaFolder, "sources", "$OEM$", "$1", "ProgramData", "Winhance", "Unattend", "Scripts");
        }
        else
        {
            return null;
        }

        var segments = destination[prefix.Length..].Split('\\');
        if (segments.Any(name => name.Length == 0 || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains(':')
            || WindowsFileNames.IsReservedDeviceName(name)))
            return null;
        if (prefix != ScriptRoot && (segments.Length > 2 || !PictureFileConstants.IsPicture(segments[^1])))
            return null;
        if (prefix == ScriptRoot && (segments.Length > 2 || (segments.Length == 2 && !segments[0].Equals("Custom", StringComparison.OrdinalIgnoreCase))))
            return null;
        return Path.Combine(root, Path.Combine(segments));
    }

    internal static byte[] BytesOf(XElement file)
    {
        if ((string?)file.Attribute("encoding") == "base64")
            return Convert.FromBase64String(file.Value);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = TextEncoding((string?)file.Attribute("path") ?? string.Empty);
        var text = file.Value.Trim().ReplaceLineEndings("\r\n");
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }

    private static Encoding TextEncoding(string destination) => Path.GetExtension(destination).ToLowerInvariant() switch
    {
        ".ps1" or ".xml" => new UTF8Encoding(true),
        ".reg" or ".vbs" or ".js" => new UnicodeEncoding(false, true),
        _ => Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage),
    };

    internal static string DecodeText(string destination, byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = new StreamReader(new MemoryStream(bytes), TextEncoding(destination), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    internal static XElement Entry(string path, byte[] bytes, bool picture) => new(X + "StagedFile",
        new XAttribute("path", path), new XAttribute("sha256", Convert.ToHexString(SHA256.HashData(bytes))),
        picture ? new XAttribute("encoding", "base64") : null);

    internal static async Task<byte[]> ReadAsync(IFileSystemService files, string mediaFolder, XElement entry, CancellationToken ct = default)
    {
        var destination = (string?)entry.Attribute("path") ?? string.Empty;
        var path = MediaPath(mediaFolder, destination);
        if (path is null || !files.FileExists(path))
            throw new IOException($"Missing staged answer-file payload: {destination}");
        CheckNotRedirected(files, path);
        var bytes = await files.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        if (!string.Equals((string?)entry.Attribute("sha256"), Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Staged answer-file payload changed: {destination}");
        return bytes;
    }

    internal static void CheckNotRedirected(IFileSystemService files, string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)!;
        var relative = Path.GetRelativePath(root, fullPath);
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            try
            {
                if (files.GetFileAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Staged answer-file payload crosses a redirected folder or file: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static async Task PublishAsync(IFileSystemService files, string stagedXml, string outputPath,
        string mediaFolder, string scratch, CancellationToken ct)
    {
        XDocument doc;
        using (var input = files.OpenRead(stagedXml))
            doc = XDocument.Load(input);
        var current = Manifest(doc);
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(outputPath)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(mediaFolder)), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The media answer file must be in the media root.");
        CheckNotRedirected(files, outputPath);
        var old = await PreviousManifestAsync(files, outputPath).ConfigureAwait(false);
        var changes = new List<(string Path, string? Source, string? OriginalHash)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in current)
        {
            ct.ThrowIfCancellationRequested();
            var destination = (string?)entry.Attribute("path") ?? string.Empty;
            var path = MediaPath(mediaFolder, destination) ?? throw new IOException($"Unsupported staged payload path: {destination}");
            if (!names.Add(path))
                throw new IOException($"Duplicate staged payload path: {destination}");
            var bytes = await ReadAsync(files, scratch, entry, ct).ConfigureAwait(false);
            CheckNotRedirected(files, path);
            string? originalHash = null;
            if (files.FileExists(path))
            {
                var existing = await files.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                if (!Owns(old, destination, existing))
                    throw new IOException($"Cannot replace an existing media file not owned by the previous answer file: {path}");
                if (existing.AsSpan().SequenceEqual(bytes))
                    continue;
                originalHash = Hash(existing);
            }
            changes.Add((path, MediaPath(scratch, destination), originalHash));
        }

        foreach (var entry in old)
        {
            ct.ThrowIfCancellationRequested();
            var destination = (string?)entry.Attribute("path") ?? string.Empty;
            var path = MediaPath(mediaFolder, destination);
            if (path is null || names.Contains(path) || !files.FileExists(path))
                continue;
            CheckNotRedirected(files, path);
            var existing = await files.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            if (Owns([entry], destination, existing) && names.Add(path))
                changes.Add((path, null, Hash(existing)));
        }

        changes.Add((outputPath, stagedXml, files.FileExists(outputPath)
            ? Hash(await files.ReadAllBytesAsync(outputPath, ct).ConfigureAwait(false)) : null));
        ct.ThrowIfCancellationRequested();

        // All ownership checks precede any change. Keep originals in the scratch directory until commit.
        var applied = new List<(string Path, string? Backup, FileAttributes Attributes)>();
        try
        {
            foreach (var (path, source, originalHash) in changes)
            {
                ct.ThrowIfCancellationRequested();
                CheckNotRedirected(files, path);
                string? actualHash = files.FileExists(path) ? Hash(await files.ReadAllBytesAsync(path, ct).ConfigureAwait(false)) : null;
                if (actualHash != originalHash)
                    throw new IOException($"Media file changed while preparing the replacement: {path}");
                string? backup = originalHash is null ? null : Path.Combine(scratch, ".backup", Path.GetRelativePath(mediaFolder, path));
                var attributes = backup is null ? FileAttributes.Normal : files.GetFileAttributes(path);
                if (backup is not null)
                {
                    files.CreateDirectory(Path.GetDirectoryName(backup)!);
                    files.CopyFile(path, backup);
                    files.SetFileAttributes(backup, FileAttributes.Normal);
                }
                try
                {
                    if (backup is not null)
                        files.SetFileAttributes(path, FileAttributes.Normal);
                    if (source is null)
                        files.DeleteFile(path);
                    else
                    {
                        files.CreateDirectory(Path.GetDirectoryName(path)!);
                        files.MoveFileReplacing(source, path);
                    }
                    applied.Add((path, backup, attributes));
                }
                catch
                {
                    // A failed same-volume move leaves the destination intact; restore its attributes, not its bytes.
                    if (backup is not null && files.FileExists(path))
                        files.SetFileAttributes(path, attributes);
                    throw;
                }
            }
        }
        catch (Exception publishError)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var (path, backup, attributes) in applied.AsEnumerable().Reverse())
            {
                try
                {
                    if (backup is not null)
                    {
                        if (files.FileExists(path))
                            files.SetFileAttributes(path, FileAttributes.Normal);
                        files.MoveFileReplacing(backup, path);
                        files.SetFileAttributes(path, attributes);
                    }
                    else if (files.FileExists(path))
                        files.DeleteFile(path);
                }
                catch (Exception ex)
                {
                    rollbackErrors.Add(ex);
                }
            }
            if (rollbackErrors.Count > 0)
                throw new MediaRollbackException(scratch, new AggregateException([publishError, .. rollbackErrors]));
            throw;
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool Owns(IEnumerable<XElement> entries, string destination, byte[] bytes) =>
        entries.Any(entry => string.Equals((string?)entry.Attribute("path"), destination, StringComparison.OrdinalIgnoreCase)
            && string.Equals((string?)entry.Attribute("sha256"), Hash(bytes), StringComparison.OrdinalIgnoreCase));

    internal static IReadOnlyList<XElement> Manifest(XDocument doc) =>
        doc.Root?.Elements(X + "Extensions").SelectMany(List).ToList() ?? [];

    private static async Task<IReadOnlyList<XElement>> PreviousManifestAsync(IFileSystemService files, string path)
    {
        if (!files.FileExists(path))
            return [];
        try
        {
            using var input = files.OpenRead(path);
            var doc = await XDocument.LoadAsync(input, LoadOptions.None, CancellationToken.None).ConfigureAwait(false);
            return doc.Root?.Elements(X + "Extensions").Where(extension => extension.Element(X + "Generator") is not null)
                .SelectMany(List).ToList() ?? [];
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }
    }

    internal static async Task StageAsync(IFileSystemService files, XDocument doc, string mediaFolder)
    {
        foreach (var file in doc.Extensions().Elements(X + "File").ToList())
        {
            var destination = (string?)file.Attribute("path") ?? string.Empty;
            var path = MediaPath(mediaFolder, destination) ?? throw new IOException($"Unsupported staged payload path: {destination}");
            var bytes = BytesOf(file);
            CheckNotRedirected(files, path);
            files.CreateDirectory(Path.GetDirectoryName(path)!);
            await files.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            file.ReplaceWith(Entry(destination, bytes, (string?)file.Attribute("encoding") == "base64"));
        }
    }
}
