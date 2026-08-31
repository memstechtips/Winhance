using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.Core.Features.Common.Exceptions;

// Raised by the USB writer for anything that stops the write after the drive has been wiped, so
// the caller can say so. A plain failure reads as "nothing happened", and the drive is blank.
public sealed class UsbMediaErasedException : Exception
{
    public UsbMediaErasedException() { }
    public UsbMediaErasedException(string message) : base(message) { }
    public UsbMediaErasedException(string message, Exception innerException) : base(message, innerException) { }

    public UsbMediaErasedException(RemovableDrive target, bool wasCancelled, Exception innerException)
        : base(innerException.Message, innerException)
    {
        Target = target;
        WasCancelled = wasCancelled;
    }

    // Null only through the standard constructors, which nothing in the app uses.
    public RemovableDrive? Target { get; }

    // True when the user cancelled rather than a step failing; the drive is equally blank either way.
    public bool WasCancelled { get; }
}
