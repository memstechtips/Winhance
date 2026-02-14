using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface ILegacyCapabilityService
{
    Task<bool> EnableCapabilityAsync(string capabilityName, string displayName = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> DisableCapabilityAsync(string capabilityName, string displayName = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
