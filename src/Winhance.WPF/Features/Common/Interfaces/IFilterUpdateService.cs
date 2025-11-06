using System.Threading.Tasks;

namespace Winhance.WPF.Features.Common.Interfaces
{
    public interface IFilterUpdateService
    {
        Task UpdateFeatureSettingsAsync(ISettingsFeatureViewModel feature);
    }
}
