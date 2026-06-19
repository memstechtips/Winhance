namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The platform reads a custom detector needs, abstracted so detectors are unit-testable without
/// touching the real registry or network. The real implementation wraps the Windows registry + network
/// services. Grows as more detectors are ported.</summary>
public interface IDetectionContext
{
    /// <summary>Read a registry value, or null if the key/value is absent.</summary>
    object? GetValue(string keyPath, string? valueName);

    /// <summary>The immediate sub-key names under a registry key (empty if the key is absent).</summary>
    string[] GetSubKeyNames(string keyPath);
}
