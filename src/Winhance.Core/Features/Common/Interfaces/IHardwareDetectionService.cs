using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IHardwareDetectionService
    {
        Task<bool> HasBatteryAsync();
        Task<bool> HasLidAsync();
        Task<bool> SupportsBrightnessControlAsync();
        Task<bool> SupportsHybridSleepAsync();
    }
}
