using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IOptionalFeatureService
{
    Task<bool> EnableFeatureAsync(string featureName, string? displayName = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> DisableFeatureAsync(string featureName, string? displayName = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
