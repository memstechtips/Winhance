namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A simple in-memory <see cref="IStateReadings"/> the discovery layer populates per setting:
/// each target key maps to its reduced value and whether it is present.</summary>
public sealed class DictReadings : IStateReadings
{
    private readonly Dictionary<string, (object? Value, bool Present)> _readings = new();

    public void Set(string targetKey, object? value, bool present) => _readings[targetKey] = (value, present);

    public bool TryGet(string targetKey, out object? value, out bool present)
    {
        if (_readings.TryGetValue(targetKey, out var entry))
        {
            value = entry.Value;
            present = entry.Present;
            return true;
        }
        value = null;
        present = false;
        return false;
    }
}
