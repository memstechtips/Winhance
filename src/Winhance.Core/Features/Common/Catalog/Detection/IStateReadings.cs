namespace Winhance.Core.Features.Common.Catalog;

public interface IStateReadings
{
    bool TryGet(string targetKey, out object? value, out bool present);
}
