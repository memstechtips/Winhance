using Windows.Win32;

namespace Winhance.Infrastructure.Features.Common.Native;

// What Windows itself holds for a format, rather than what the current user has edited: LOCALE_NOUSEROVERRIDE asks
// for the locale's own value, which is exactly what a format apply has to write. Measured against Windows' own
// writes for en-ZA, hi-IN and ar-SA on 2026-09-21 - all nine values matched, where .NET's CultureInfo did not.
internal static class LocaleInfo
{
    // The longest answer among the values read through here is a ten-character digit set; 80 is the cap
    // GetLocaleInfoEx documents for a locale string.
    private const int MaxLength = 80;

    // Null when Windows has no answer, which leaves the machine's own value standing.
    public static string? Read(string localeName, uint lcType)
    {
        Span<char> buffer = stackalloc char[MaxLength];
        int written = PInvoke.GetLocaleInfoEx(localeName, lcType | PInvoke.LOCALE_NOUSEROVERRIDE, buffer);

        // The count includes the terminating null, so a value Windows keeps empty comes back as 1.
        return written > 0 ? new string(buffer[..(written - 1)]) : null;
    }
}
