namespace Winhance.Core.Features.WimUtil.Models;

public sealed record UsbWriteLayout(
    bool RequiresSplit,
    long TotalPayloadBytes,
    bool ExceedsFat32Ceiling);
