using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IConfigurationService
    {
        Task ExportConfigurationAsync();
        Task ImportConfigurationAsync();
        Task ImportRecommendedConfigurationAsync();
    }
}
