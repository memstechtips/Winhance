using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting);
}
