namespace Winhance.Infrastructure.Features.Common.Services;

internal interface IServiceStartControl
{
    void Apply(string serviceName, int start);
}
