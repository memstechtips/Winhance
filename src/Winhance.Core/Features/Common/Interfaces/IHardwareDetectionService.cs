using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IHardwareDetectionService
    {
        Task<bool> HasBatteryAsync();
        Task<int?> GetBatteryPercentageAsync();
        Task<bool> IsRunningOnBatteryAsync();
        Task<bool> HasLidAsync();
        Task<bool> SupportsBrightnessControlAsync();
    }
}
