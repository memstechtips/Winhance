using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Rewrites the live roots the catalog writes to onto the hives loaded from the mounted image, then lets the live
// registry service do the work. A root with no loaded counterpart is refused rather than written to this PC.
internal sealed class OfflineRegistry(IWindowsRegistryService live, IOfflineHiveSession session, IRegistryHiveNative hives, ILogService log) : IWindowsRegistryService
{
    private readonly HashSet<string> _unmapped = new(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyCollection<string> Unmapped => _unmapped;

    internal int Refusals { get; private set; }

    internal string? Map(string path)
    {
        var parts = path.Split('\\', 2);
        if (parts.Length < 2)
        {
            return null;
        }

        var hive = parts[0].TrimEnd(':');
        var rest = parts[1].Trim('\\');
        if (hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) || hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase))
        {
            return MapMachine(rest);
        }

        // The per-user classes live in UsrClass.dat, which is not loaded, and Windows ignores a copy under NTUSER.DAT.
        if (hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) || hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase))
        {
            return IsUnder(rest, @"Software\Classes") ? null : Join(session.DefaultUserRoot, rest);
        }

        return null;
    }

    private string? MapMachine(string rest)
    {
        var (head, tail) = SplitFirst(rest);
        if (head.Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase))
        {
            return Join(session.SoftwareRoot, tail);
        }

        if (head.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            // CurrentControlSet is a link the offline hive does not have; ControlSet001 is what it points at.
            var (set, afterSet) = SplitFirst(tail);
            return set.Equals("CurrentControlSet", StringComparison.OrdinalIgnoreCase) || set.Equals("ControlSet001", StringComparison.OrdinalIgnoreCase)
                ? Join(session.SystemRoot, "ControlSet001", afterSet)
                : Join(session.SystemRoot, tail);
        }

        return null;
    }

    private static (string Head, string Tail) SplitFirst(string path)
    {
        var parts = path.Split('\\', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    private static bool IsUnder(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase) || path.StartsWith(prefix + @"\", StringComparison.OrdinalIgnoreCase);

    private static string Join(params string[] parts) => string.Join('\\', parts.Where(p => p.Length > 0));

    private bool TryMap(string path, out string mapped)
    {
        mapped = Map(path) ?? string.Empty;
        if (mapped.Length > 0)
        {
            return true;
        }

        Refusals++;
        var (hive, rest) = SplitFirst(path);
        var (first, _) = SplitFirst(rest);
        if (_unmapped.Add($@"{hive}\{first}"))
        {
            log.LogWarning($"{path} has no matching hive in the mounted image, so it is left for setup");
        }
        return false;
    }

    private T On<T>(string path, Func<string, T> run, T unmapped) => TryMap(path, out var mapped) ? run(mapped) : unmapped;

    // For keys the image's hive guards for SYSTEM alone. A refusal counts like an unmapped root: the setting is left for setup.
    internal bool SetProtectedValue(string keyPath, string valueName, object value, RegistryValueKind kind)
    {
        if (!TryMap(keyPath, out var mapped))
        {
            return false;
        }

        var error = hives.SetValue(SplitFirst(mapped).Tail, valueName, value, kind);
        if (error == 0)
        {
            return true;
        }

        Refusals++;
        log.LogWarning($"Windows refused the write to '{keyPath}\\{valueName}' in the mounted image (error {error}), so it is left for setup");
        return false;
    }

    public bool SetValue(string keyPath, string valueName, object value, RegistryValueKind kind) =>
        On(keyPath, mapped => live.SetValue(mapped, valueName, value, kind), false);

    public object? GetValue(string keyPath, string valueName) =>
        On(keyPath, mapped => live.GetValue(mapped, valueName), null);

    public string? GetUnexpandedString(string keyPath, string valueName) =>
        On(keyPath, mapped => live.GetUnexpandedString(mapped, valueName), null);

    public RegistryValueKind? GetValueKind(string keyPath, string valueName) =>
        On(keyPath, mapped => live.GetValueKind(mapped, valueName), null);

    // The live guard matches the real branch names, which the mount names would hide from it.
    public bool DeleteKey(string keyPath)
    {
        if (!TryMap(keyPath, out var mapped))
        {
            return false;
        }

        if (WindowsRegistryService.IsProtected(SplitFirst(keyPath).Tail))
        {
            log.LogWarning($"Refusing to delete protected registry key '{keyPath}' in the mounted image");
            return false;
        }
        return live.DeleteKey(mapped);
    }

    public bool DeleteValue(string keyPath, string valueName) =>
        On(keyPath, mapped => live.DeleteValue(mapped, valueName), false);

    public bool KeyExists(string keyPath) =>
        On(keyPath, live.KeyExists, false);

    public bool ValueExists(string keyPath, string valueName) =>
        On(keyPath, mapped => live.ValueExists(mapped, valueName), false);

    public string[] GetSubKeyNames(string keyPath) =>
        On(keyPath, live.GetSubKeyNames, Array.Empty<string>());

    public string[] GetValueNames(string keyPath) =>
        On(keyPath, live.GetValueNames, Array.Empty<string>());

    public bool CreateKey(string keyPath) =>
        On(keyPath, live.CreateKey, false);

    public bool LockRegistryKey(string keyPath) =>
        On(keyPath, live.LockRegistryKey, false);

    public bool UnlockRegistryKey(string keyPath) =>
        On(keyPath, live.UnlockRegistryKey, false);

    public bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit) =>
        On(keyPath, mapped => live.ModifyBinaryBit(mapped, valueName, byteIndex, bitMask, setBit), false);

    public bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue) =>
        On(keyPath, mapped => live.ModifyBinaryByte(mapped, valueName, byteIndex, newValue), false);

    public bool SetCompositeSubValue(string keyPath, string valueName, string compositeKey, string? subValue) =>
        On(keyPath, mapped => live.SetCompositeSubValue(mapped, valueName, compositeKey, subValue), false);
}
