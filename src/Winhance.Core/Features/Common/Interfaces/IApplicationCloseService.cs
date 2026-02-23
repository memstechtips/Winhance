namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IApplicationCloseService
    {
        Func<Task>? BeforeShutdown { get; set; }
        Task<bool> CheckOperationsAndCloseAsync();
    }
}
