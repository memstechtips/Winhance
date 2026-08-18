using Microsoft.Win32;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsRegistryService
{
    bool SetValue(string keyPath, string valueName, object value, RegistryValueKind kind);
    object? GetValue(string keyPath, string valueName);

    // Diagnostic only - reports a value stored under a type its catalog target cannot read.
    RegistryValueKind? GetValueKind(string keyPath, string valueName);
    bool DeleteKey(string keyPath);
    bool DeleteValue(string keyPath, string valueName);

    bool KeyExists(string keyPath);
    bool ValueExists(string keyPath, string valueName);
    string[] GetSubKeyNames(string keyPath);

    // --- Apply primitives, exposed for the catalog IStateWriter (see WindowsStateWriter).

    bool CreateKey(string keyPath);

    // Administrators retain full control; SYSTEM loses write so Windows cannot revert the protective value just written.
    bool LockRegistryKey(string keyPath);

    bool UnlockRegistryKey(string keyPath);

    bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit);

    bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue);

    // Sub-keys compare OrdinalIgnoreCase; the merged string carries a trailing ";".
    bool SetCompositeSubValue(string keyPath, string valueName, string compositeKey, string? subValue);
}
