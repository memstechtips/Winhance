using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ISettingOperationExecutor
    {
        Task<OperationResult> ApplySettingOperationsAsync(SettingDefinition setting, bool enable, object? value);
    }
}
