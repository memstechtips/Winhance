namespace Winhance.Core.Features.Common.Interfaces;

public interface IStateReadings
{
    bool TryGet(string targetKey, out object? value, out bool present);
}
