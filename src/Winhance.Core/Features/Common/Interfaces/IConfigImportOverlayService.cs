namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigImportOverlayService
{
    void ShowOverlay(string statusText, string? detailText = null);
    void UpdateStatus(string statusText, string? detailText = null);
    void HideOverlay();
}
