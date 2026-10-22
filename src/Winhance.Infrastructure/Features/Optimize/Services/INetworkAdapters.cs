namespace Winhance.Infrastructure.Features.Optimize.Services;

internal interface INetworkAdapters
{
    IEnumerable<string> Ids();

    bool NudgeDnsClient();
}
