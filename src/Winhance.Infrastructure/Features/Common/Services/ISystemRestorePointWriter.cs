namespace Winhance.Infrastructure.Features.Common.Services;

// SRSetRestorePointW plus the registry frequency-throttle dance around it, behind one seam. The
// finally-block throttle restore must run whatever the native call does, so splitting the two would
// leave a fake able to skip a real side effect the real implementation can never skip.
// SequenceNumber is null when the point may not be this call's own (the throttle stayed on), so it is never removed.
internal interface ISystemRestorePointWriter
{
    (bool Success, int StatusCode, uint? SequenceNumber) CreateRestorePoint(string description);

    int RemoveRestorePoint(uint sequenceNumber);
}
