using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IApplicationCloseService
    {
        Func<Task>? BeforeShutdown { get; set; }
        Task<OperationResult> CheckOperationsAndCloseAsync();
    }
}
