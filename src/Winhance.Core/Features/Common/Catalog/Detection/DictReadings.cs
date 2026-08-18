namespace Winhance.Core.Features.Common.Catalog;

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
