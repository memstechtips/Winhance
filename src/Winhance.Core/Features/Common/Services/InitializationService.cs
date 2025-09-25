using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Services
{
    public class InitializationService : IInitializationService
    {
        private readonly HashSet<string> _initializingFeatures = new();
        private readonly object _lock = new();

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
            }
        }

        public void CompleteFeatureInitialization(string featureName)
        {
            lock (_lock)
            {
                _initializingFeatures.Remove(featureName);
            }
        }
    }
}