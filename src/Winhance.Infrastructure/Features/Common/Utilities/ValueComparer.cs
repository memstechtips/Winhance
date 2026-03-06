namespace Winhance.Infrastructure.Features.Common.Utilities;

/// <summary>
/// Shared value comparison logic for registry/combobox values.
/// Handles cross-type comparisons (byte/int, byte arrays, etc.).
/// </summary>
internal static class ValueComparer
{
    public static bool ValuesAreEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        if (value1 is byte[] bytes1 && value2 is byte[] bytes2)
        {
            return bytes1.SequenceEqual(bytes2);
        }

        if (value1 is byte b1 && value2 is byte b2)
        {
            return b1 == b2;
        }

        if (value1 is byte byteVal1 && value2 is int intVal2)
        {
            return byteVal1 == intVal2;
        }

        if (value1 is int intVal1 && value2 is byte byteVal2)
        {
            return intVal1 == byteVal2;
        }

        return value1.Equals(value2);
    }
}
