using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsUIManagementService
    {
        void RestartExplorer();
        bool IsProcessRunning(string processName);
        void KillProcess(string processName);
        void RefreshDesktop();
        Task<bool> RefreshWindowsGUI(bool killExplorer = true);
    }
}