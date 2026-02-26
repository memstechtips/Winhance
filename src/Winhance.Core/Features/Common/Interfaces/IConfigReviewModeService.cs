using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Manages the lifecycle of Config Review Mode (enter/exit/query).
/// </summary>
public interface IConfigReviewModeService
{
    bool IsInReviewMode { get; }
    UnifiedConfigurationFile? ActiveConfig { get; }
    Task EnterReviewModeAsync(UnifiedConfigurationFile config);
    void ExitReviewMode();
    event EventHandler? ReviewModeChanged;
}
