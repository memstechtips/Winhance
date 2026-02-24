using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ISettingApplicationService
    {
        Task<OperationResult> ApplySettingAsync(ApplySettingRequest request);
        Task ApplyRecommendedSettingsForDomainAsync(string settingId);
    }
}
