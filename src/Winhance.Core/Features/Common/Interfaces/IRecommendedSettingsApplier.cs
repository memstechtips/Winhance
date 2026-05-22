using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IRecommendedSettingsApplier
{
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService settingApplicationService);
}
