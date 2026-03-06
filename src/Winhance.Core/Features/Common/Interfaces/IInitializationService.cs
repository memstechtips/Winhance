namespace Winhance.Core.Features.Common.Interfaces;

public interface IInitializationService
{
    bool IsGloballyInitializing { get; }
    void StartFeatureInitialization(string featureName);
    void CompleteFeatureInitialization(string featureName);
}
