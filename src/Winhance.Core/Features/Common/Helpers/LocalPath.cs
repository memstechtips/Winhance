namespace Winhance.Core.Features.Common.Helpers;

public static class LocalPath
{
    // A network path makes Windows open an SMB connection and send the user's credentials to the named host. UNC,
    // \\?\ and \\.\ paths all start with two separators, and Windows reads / as a separator too.
    public static bool IsLocal(string path) =>
        Path.IsPathFullyQualified(path) && !(path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]));

    private static bool IsSeparator(char c) => c is '\\' or '/';
}
