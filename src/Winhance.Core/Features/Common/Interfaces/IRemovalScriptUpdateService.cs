using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IRemovalScriptUpdateService
    {
        Task CheckAndUpdateScriptsAsync();
    }
}
