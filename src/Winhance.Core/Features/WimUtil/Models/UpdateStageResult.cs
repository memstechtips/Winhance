namespace Winhance.Core.Features.WimUtil.Models;

public enum UpdateStageResult
{
    Staged,
    DownloadFailed,
    HashMismatch,
    SignatureInvalid
}
