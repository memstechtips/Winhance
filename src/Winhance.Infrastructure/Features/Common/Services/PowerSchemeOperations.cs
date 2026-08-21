using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class PowerSchemeOperations : IPowerSchemeOperations
{
    public uint DeleteScheme(Guid schemeGuid) =>
        (uint)PInvoke.PowerDeleteScheme(null, schemeGuid);

    public unsafe uint DuplicateScheme(Guid sourceGuid, Guid? desiredGuid, out Guid destinationGuid)
    {
        // DestinationSchemeGuid is a GUID**: pointing it at our own GUID asks for that GUID, leaving it
        // null asks the API to allocate one. Whichever happened, the answer is read back from the same
        // slot afterwards - the API is free to hand back a different GUID and the caller must know.
        Guid wanted = desiredGuid ?? Guid.Empty;
        Guid* slot = desiredGuid.HasValue ? &wanted : null;

        var result = PInvoke.PowerDuplicateScheme(null, sourceGuid, ref slot);

        destinationGuid = result == WIN32_ERROR.ERROR_SUCCESS && slot is not null
            ? *slot
            : Guid.Empty;

        // Only free what the API allocated; our own GUID lives on the stack.
        if (slot is not null && slot != &wanted)
            PInvoke.LocalFree((HLOCAL)(IntPtr)slot);

        return (uint)result;
    }

    public uint SetActiveScheme(Guid schemeGuid) =>
        (uint)PInvoke.PowerSetActiveScheme(null, schemeGuid);

    public uint WriteFriendlyName(Guid schemeGuid, string name) =>
        (uint)PInvoke.PowerWriteFriendlyName(null, schemeGuid, null, null, NullTerminated(name));

    public uint WriteDescription(Guid schemeGuid, string description) =>
        (uint)PInvoke.PowerWriteDescription(null, schemeGuid, null, null, NullTerminated(description));

    // The API takes a byte buffer sized in bytes, and Windows expects the trailing NUL to be part of it.
    private static ReadOnlySpan<byte> NullTerminated(string value) =>
        Encoding.Unicode.GetBytes(value + "\0");
}
