namespace Winhance.Core.Features.AdvancedTools.Models;

public sealed record UsbWriteLayout(
    bool RequiresSplit,
    int SplitSizeMb,
    long TotalPayloadBytes,
    bool ExceedsFat32Ceiling);
