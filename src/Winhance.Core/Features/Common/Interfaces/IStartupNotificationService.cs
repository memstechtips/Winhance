namespace Winhance.Core.Features.Common.Interfaces;

public interface IStartupNotificationService
{
    Task ShowFirstLaunchRestoreOfferAsync();
}
