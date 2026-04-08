using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISettingOperationExecutor
{
    /// <param name="resetToDefault">When true, uses DisabledValue[1] instead of DisabledValue[0] for registry settings (used when parent cascades disable to children).</param>
    Task<OperationResult> ApplySettingOperationsAsync(SettingDefinition setting, bool enable, object? value, bool resetToDefault = false);
}
