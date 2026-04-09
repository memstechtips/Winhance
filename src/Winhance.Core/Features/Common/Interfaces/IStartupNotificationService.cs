using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IStartupNotificationService
{
    /// <summary>
    /// Shows the first-launch dialog offering to create a restore point.
    /// Only shows if this is the first launch and the offer hasn't been shown before.
    /// </summary>
    Task ShowFirstLaunchRestoreOfferAsync();
}
