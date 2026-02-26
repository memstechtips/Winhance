using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Services;

public class InitializationService : IInitializationService
{
    private readonly HashSet<string> _initializingFeatures = new();
    private readonly object _lock = new();
    private readonly ILogService _logService;

    public InitializationService(ILogService logService)
    {
        _logService = logService;
    }

    public bool IsGloballyInitializing
    {
        get
        {
            lock (_lock)
            {
                return _initializingFeatures.Count > 0;
            }
        }
    }

    public void StartFeatureInitialization(string featureName)
    {
        lock (_lock)
        {
            _initializingFeatures.Add(featureName);
            _logService.Log(LogLevel.Info, $"[InitializationService] Started initialization for '{featureName}' - Total initializing: {_initializingFeatures.Count}, Features: [{string.Join(", ", _initializingFeatures)}]");
        }
    }

    public void CompleteFeatureInitialization(string featureName)
    {
        lock (_lock)
        {
            _initializingFeatures.Remove(featureName);
            _logService.Log(LogLevel.Info, $"[InitializationService] Completed initialization for '{featureName}' - Total initializing: {_initializingFeatures.Count}, Features: [{string.Join(", ", _initializingFeatures)}]");
        }
    }
}
