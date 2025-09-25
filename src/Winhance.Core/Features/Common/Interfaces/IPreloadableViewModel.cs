using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IPreloadableViewModel : IFeatureViewModel
    {
        Task PreloadFeaturesAsync();
    }
}