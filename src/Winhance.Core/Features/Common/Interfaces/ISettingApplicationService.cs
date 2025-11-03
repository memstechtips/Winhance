using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ISettingApplicationService
    {
        Task ApplySettingAsync(string settingId, bool enable, object? value = null, bool checkboxResult = false, string? commandString = null, bool applyRecommended = false, bool skipValuePrerequisites = false);
    }
}