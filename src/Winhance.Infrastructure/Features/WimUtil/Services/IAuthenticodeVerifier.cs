namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IAuthenticodeVerifier
{
    bool IsMicrosoftSigned(string path);
}
