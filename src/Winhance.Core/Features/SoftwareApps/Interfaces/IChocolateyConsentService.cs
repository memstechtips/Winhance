using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IChocolateyConsentService
{
    Task<bool> RequestConsentAsync();
}
