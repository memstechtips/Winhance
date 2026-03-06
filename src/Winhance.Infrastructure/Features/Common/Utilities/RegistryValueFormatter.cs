using System;
using System.Linq;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal static class RegistryValueFormatter
{
    public static string? Format(object? value, RegistrySetting? registrySetting)
    {
        if (value == null)
            return null;

        if (value is byte[] bytes && registrySetting != null)
        {
            if (bytes.Length == 0)
                return "(empty)";

            if (registrySetting.BinaryByteIndex.HasValue && bytes.Length > registrySetting.BinaryByteIndex.Value)
            {
                var targetByte = bytes[registrySetting.BinaryByteIndex.Value];

                if (registrySetting.BitMask.HasValue)
                {
                    var isSet = (targetByte & registrySetting.BitMask.Value) != 0;
                    return isSet ? "1" : "0";
                }

                return targetByte.ToString();
            }

            return string.Join(" ", bytes);
        }

        // Extract sub-value for CompositeStringKey settings
        if (registrySetting?.CompositeStringKey != null && value is string compositeStr)
        {
            foreach (var entry in compositeStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = entry.IndexOf('=');
                if (eqIndex > 0 &&
                    string.Equals(entry[..eqIndex], registrySetting.CompositeStringKey, StringComparison.OrdinalIgnoreCase))
                {
                    return entry[(eqIndex + 1)..];
                }
            }
            return registrySetting.DefaultValue?.ToString();
        }

        return value.ToString()!;
    }
}
