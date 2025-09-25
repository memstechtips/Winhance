using System.Threading.Tasks;

namespace Winhance.WPF.Features.Common.Interfaces
{
    public interface IAppFeatureViewModel
    {
        Task LoadItemsAsync();
    }
}