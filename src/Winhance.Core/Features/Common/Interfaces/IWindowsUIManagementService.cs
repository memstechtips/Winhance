using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsUIManagementService
    {
        bool IsProcessRunning(string processName);
        void KillProcess(string processName);
        Task<bool> RefreshWindowsGUI(bool killExplorer = true);
    }
}