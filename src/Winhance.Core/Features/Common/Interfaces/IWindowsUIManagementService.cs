using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsUIManagementService
    {
        bool IsConfigImportMode { get; set; }
        bool IsProcessRunning(string processName);
        void KillProcess(string processName);
        Task<bool> RefreshWindowsGUI(bool killExplorer = true);
    }
}