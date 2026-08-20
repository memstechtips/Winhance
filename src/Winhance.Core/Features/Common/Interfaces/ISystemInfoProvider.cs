using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISystemInfoProvider
{
    SystemInfo Collect();
}
