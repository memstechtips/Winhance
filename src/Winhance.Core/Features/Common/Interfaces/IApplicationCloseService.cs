namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IApplicationCloseService
    {
        Func<Task> BeforeShutdown { get; set; }
        Task<bool> CheckOperationsAndCloseAsync();
        Task CloseApplicationWithSupportDialogAsync();
        Task SaveDontShowSupportPreferenceAsync(bool dontShow);
        Task<bool> ShouldShowSupportDialogAsync();
    }
}
