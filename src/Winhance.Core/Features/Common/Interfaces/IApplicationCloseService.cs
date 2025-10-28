namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IApplicationCloseService
    {
        Task<bool> CheckOperationsAndCloseAsync();
        Task CloseApplicationWithSupportDialogAsync();
        Task SaveDontShowSupportPreferenceAsync(bool dontShow);
        Task<bool> ShouldShowSupportDialogAsync();
    }
}
