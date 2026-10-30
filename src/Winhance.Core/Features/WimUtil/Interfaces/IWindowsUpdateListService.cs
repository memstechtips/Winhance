using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IWindowsUpdateListService
{
    // The update to offer for media of this build, UBR and architecture (x64, arm64); null when the list
    // has nothing newer, or no list could be loaded. Never throws for a missing or bad list.
    Task<WindowsUpdate?> FindUpdateAsync(int build, int ubr, string architecture, CancellationToken cancellationToken = default);
}
