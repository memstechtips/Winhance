using System;
using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Wraps PowrProf.dll plan-level P/Invoke calls behind an injectable interface.
/// </summary>
public class PowerSchemeOperations : IPowerSchemeOperations
{
    public uint DeleteScheme(Guid schemeGuid)
    {
        return PowerProf.PowerDeleteScheme(IntPtr.Zero, ref schemeGuid);
    }

    public uint DuplicateScheme(Guid sourceGuid, out Guid destinationGuid)
    {
        var result = PowerProf.PowerDuplicateScheme(IntPtr.Zero, ref sourceGuid, out var destPtr);
        if (result == PowerProf.ERROR_SUCCESS)
        {
            destinationGuid = Marshal.PtrToStructure<Guid>(destPtr);
            PowerProf.LocalFree(destPtr);
        }
        else
        {
            destinationGuid = Guid.Empty;
        }
        return result;
    }

    public uint SetActiveScheme(Guid schemeGuid)
    {
        return PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
    }

    public uint WriteFriendlyName(Guid schemeGuid, string name)
    {
        var nameBytes = (uint)(name.Length * 2 + 2);
        return PowerProf.PowerWriteFriendlyName(
            IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, name, nameBytes);
    }

    public uint WriteDescription(Guid schemeGuid, string description)
    {
        var descBytes = (uint)(description.Length * 2 + 2);
        return PowerProf.PowerWriteDescription(
            IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, description, descBytes);
    }
}
