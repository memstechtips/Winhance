using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Infrastructure.Features.Optimize.Services;

internal interface IDnsSettingsService
{
    bool SetDns(IReadOnlyList<DnsServer> servers);
}
