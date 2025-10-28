using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IStartupNotificationService
    {
        Task ShowBackupNotificationAsync(BackupResult result);
        void ShowMigrationNotification(ScriptMigrationResult result);
    }
}
