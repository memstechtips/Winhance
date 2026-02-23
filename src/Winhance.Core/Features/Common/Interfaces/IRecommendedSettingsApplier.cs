using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IRecommendedSettingsApplier
    {
        Task ApplyRecommendedSettingsForDomainAsync(string settingId, ISettingApplicationService settingApplicationService);
    }
}
