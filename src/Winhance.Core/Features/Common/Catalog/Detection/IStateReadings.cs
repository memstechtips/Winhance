namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Abstracts the live reading for a target key so detection is testable without a registry.
/// <c>present</c> is false when the key/value is absent on the system.</summary>
public interface IStateReadings
{
    bool TryGet(string targetKey, out object? value, out bool present);
}
