namespace Winhance.Core.Features.AdvancedTools.Models;

public sealed record UsbWriteLayout(
    bool RequiresSplit,
    long TotalPayloadBytes,
    bool ExceedsFat32Ceiling);
