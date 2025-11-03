using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsUIManagementService
    {
        bool IsConfigImportMode { get; set; }
        void RestartExplorer();
        bool IsProcessRunning(string processName);
        void KillProcess(string processName);
        void RefreshDesktop();
        Task<bool> RefreshWindowsGUI(bool killExplorer = true);
    }
}