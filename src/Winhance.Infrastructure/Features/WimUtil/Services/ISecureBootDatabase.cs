namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface ISecureBootDatabase
{
    // Null when there is no Secure Boot database to read: legacy BIOS, or firmware without Secure Boot.
    bool? RevokesPca2011();
}
