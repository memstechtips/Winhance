namespace Winhance.Infrastructure.Features.Common.Services;

// SRSetRestorePointW plus the registry frequency-throttle dance around it, behind one seam. The
// finally-block throttle restore must run whatever the native call does, so splitting the two would
// leave a fake able to skip a real side effect the real implementation can never skip.
internal interface ISystemRestorePointWriter
{
    (bool Success, int StatusCode) CreateRestorePoint(string description);
}
