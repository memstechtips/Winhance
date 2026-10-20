namespace Winhance.Core.Features.Common.Constants;

public static class PictureFileConstants
{
    // Every picture type the Windows background picker offers. A config is someone else's file: nothing else lands.
    private static readonly string[] Extensions =
    [
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".dib", ".gif", ".tif", ".tiff", ".wdp", ".jxr", ".webp",
        ".heic", ".heif", ".hif", ".avif",
    ];

    private static readonly HashSet<string> ExtensionSet = new(Extensions, StringComparer.OrdinalIgnoreCase);

    public static readonly string FilePattern = string.Join(';', Extensions.Select(extension => "*" + extension));

    public static bool IsPicture(string path) => ExtensionSet.Contains(Path.GetExtension(path));
}
