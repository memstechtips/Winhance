using System.Text;

namespace Winhance.Core.Features.Autounattend;

public static class AccountPasswords
{
    // Windows reads an obscured password as base64 of the UTF-16LE bytes of the password plus the element's name.
    // Microsoft never states the rule; it comes from decoding three of its documented examples.
    public static string Obscure(string password, string elementName) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(password + elementName));

    public static bool TryDeobscure(string? value, string elementName, out string password)
    {
        password = string.Empty;
        if (string.IsNullOrEmpty(value))
            return false;

        var bytes = new byte[value.Length / 4 * 3 + 3];
        if (!Convert.TryFromBase64String(value, bytes, out var count))
            return false;

        var decoded = Encoding.Unicode.GetString(bytes, 0, count);
        if (!decoded.EndsWith(elementName, StringComparison.Ordinal))
            return false;

        password = decoded[..^elementName.Length];
        return true;
    }
}
