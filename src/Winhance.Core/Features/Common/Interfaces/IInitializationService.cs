namespace Winhance.Core.Features.Common.Interfaces;

public interface IInitializationService
{
    void StartFeatureInitialization(string featureName);
    void CompleteFeatureInitialization(string featureName);
}
