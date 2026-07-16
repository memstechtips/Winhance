using Microsoft.Win32;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsRegistryService
{
    bool SetValue(string keyPath, string valueName, object value, RegistryValueKind kind);
    object? GetValue(string keyPath, string valueName);
    bool DeleteKey(string keyPath);
    bool DeleteValue(string keyPath, string valueName);

    bool KeyExists(string keyPath);
    bool ValueExists(string keyPath, string valueName);
    string[] GetSubKeyNames(string keyPath);
    Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string? valueName)> queries);

    // --- Apply primitives (exposed for the new catalog IStateWriter; the old ApplySetting calls the same methods,
    //     so its byte-for-byte behaviour is unchanged). Extract-refactor: see WindowsStateWriter.

    /// <summary>Creates the registry key (and parents) if it does not already exist; true if it exists afterwards.</summary>
    bool CreateKey(string keyPath);

    /// <summary>ACL-locks a key read-only for SYSTEM (Administrators retain full control) so Windows cannot revert
    /// the protective value just written.</summary>
    bool LockRegistryKey(string keyPath);

    /// <summary>Restores default permissions on a key (re-enables inheritance, SYSTEM full control) before writing it.</summary>
    bool UnlockRegistryKey(string keyPath);

    /// <summary>Sets or clears a single bit within one byte of a REG_BINARY value, preserving the other bytes
    /// (creates a 12-byte default array when the value is absent or too short).</summary>
    bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit);

    /// <summary>Overwrites a single byte of a REG_BINARY value, preserving the other bytes (creates a 12-byte
    /// default array when the value is absent or too short).</summary>
    bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue);

    /// <summary>Read-merge-write of one sub-key inside a packed ";"-delimited "key=value" REG_SZ value: re-reads the
    /// current composite, sets (or, when <paramref name="subValue"/> is null, removes) the given sub-key, and writes
    /// the merged string back. Sub-keys are OrdinalIgnoreCase; the merged string carries a trailing ";".</summary>
    bool SetCompositeSubValue(string keyPath, string valueName, string compositeKey, string? subValue);
}
