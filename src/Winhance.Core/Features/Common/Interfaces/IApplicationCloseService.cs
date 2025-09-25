namespace Winhance.Core.Features.Common.Interfaces
{

    public interface IApplicationCloseService
    {
        Task CloseApplicationWithSupportDialogAsync();
        Task SaveDontShowSupportPreferenceAsync(bool dontShow);
        Task<bool> ShouldShowSupportDialogAsync();
    }
}
