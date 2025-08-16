using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.Common.Extensions.DI
{
    /// <summary>
    /// Validates dependency injection configuration for common issues and best practices.
    /// Helps catch configuration problems early in development.
    /// </summary>
    public class DIConfigurationValidator
    {
        /// <summary>
        /// Validates the complete service configuration.
        /// </summary>
        /// <param name="services">The service collection to validate</param>
        /// <returns>Validation results with errors and warnings</returns>
        public ValidationResults ValidateConfiguration(IServiceCollection services)
        {
            var results = new ValidationResults();

            ValidateDuplicateRegistrations(services, results);
            ValidateServiceLifetimes(services, results);
            ValidateCircularDependencies(services, results);
            ValidateMissingRegistrations(services, results);
            ValidateFactoryPatterns(services, results);

            return results;
        }

        /// <summary>
        /// Validates for duplicate service registrations.
        /// </summary>
        private static void ValidateDuplicateRegistrations(
            IServiceCollection services,
            ValidationResults results
        )
        {
            var duplicates = services
                .GroupBy(s => s.ServiceType)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var duplicate in duplicates)
            {
                // Some services legitimately have multiple registrations (e.g., collections)
                if (IsLegitimateMultipleRegistration(duplicate.Key))
                {
                    results.AddWarning(
                        $"Multiple registrations for {duplicate.Key.Name} "
                            + $"({duplicate.Count()} registrations) - verify this is intentional"
                    );
                }
                else
                {
                    results.AddError(
                        $"Duplicate registration detected for {duplicate.Key.Name} "
                            + $"({duplicate.Count()} registrations)"
                    );
                }
            }
        }

        /// <summary>
        /// Validates that services are using appropriate lifetimes.
        /// </summary>
        private static void ValidateServiceLifetimes(
            IServiceCollection services,
            ValidationResults results
        )
        {
            foreach (var service in services)
            {
                if (service.ServiceType.IsInterface)
                {
                    var recommendedLifetime = ServiceLifetimeStrategy.GetRecommendedLifetime(
                        service.ServiceType
                    );

                    if (service.Lifetime != recommendedLifetime)
                    {
                        var rationale = ServiceLifetimeStrategy.GetLifetimeRationale(
                            service.ServiceType
                        );
                        results.AddWarning(
                            $"Service {service.ServiceType.Name} is registered as "
                                + $"{service.Lifetime} but {rationale}"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Validates for potential circular dependency issues.
        /// This is a basic validation - full circular dependency detection requires building the container.
        /// </summary>
        private static void ValidateCircularDependencies(
            IServiceCollection services,
            ValidationResults results
        )
        {
            // Look for obvious circular dependency patterns
            var implementationTypes = services
                .Where(s => s.ImplementationType != null)
                .Select(s => s.ImplementationType!)
                .Distinct()
                .ToList();

            foreach (var implementationType in implementationTypes)
            {
                var constructors = implementationType.GetConstructors();
                var primaryConstructor = constructors
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (primaryConstructor == null)
                    continue;

                var dependencies = primaryConstructor
                    .GetParameters()
                    .Select(p => p.ParameterType)
                    .ToList();

                // Check if any dependency could create a circular reference
                foreach (var dependency in dependencies)
                {
                    if (CouldCreateCircularDependency(implementationType, dependency, services))
                    {
                        results.AddWarning(
                            $"Potential circular dependency: {implementationType.Name} "
                                + $"depends on {dependency.Name}"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Validates for missing service registrations based on common patterns.
        /// </summary>
        private static void ValidateMissingRegistrations(
            IServiceCollection services,
            ValidationResults results
        )
        {
            var registeredTypes = services.Select(s => s.ServiceType).ToHashSet();

            // Check for common missing registrations
            var requiredServices = new[]
            {
                "ILogService",
                "IRegistryService",
                "ISystemServices",
                "IEventBus",
            };

            foreach (var requiredService in requiredServices)
            {
                if (!registeredTypes.Any(t => t.Name == requiredService))
                {
                    results.AddError($"Required service {requiredService} is not registered");
                }
            }
        }

        /// <summary>
        /// Validates for factory pattern anti-patterns.
        /// </summary>
        private static void ValidateFactoryPatterns(
            IServiceCollection services,
            ValidationResults results
        )
        {
            var factoryRegistrations = services
                .Where(s => s.ImplementationFactory != null)
                .ToList();

            if (factoryRegistrations.Count > services.Count * 0.3) // More than 30% factory registrations
            {
                results.AddWarning(
                    $"High number of factory registrations ({factoryRegistrations.Count}) "
                        + "detected - consider using constructor injection instead"
                );
            }

            foreach (var factory in factoryRegistrations)
            {
                if (factory.ServiceType.Name.EndsWith("ViewModel"))
                {
                    results.AddWarning(
                        $"ViewModel {factory.ServiceType.Name} uses factory registration - "
                            + "consider using constructor injection for better testability"
                    );
                }
            }
        }

        /// <summary>
        /// Determines if a service type legitimately supports multiple registrations.
        /// </summary>
        private static bool IsLegitimateMultipleRegistration(Type serviceType)
        {
            // Services that commonly have multiple registrations
            return serviceType.Name.Contains("Collection")
                || serviceType.Name == "IVerificationMethod"
                || serviceType.Name == "IScriptContentModifier"
                || serviceType.Name == "ISettingApplicationStrategy"
                || serviceType.Name == "ISectionConfigurationApplier"
                || serviceType.IsGenericType
                    && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>);
        }

        /// <summary>
        /// Basic heuristic to detect potential circular dependencies.
        /// </summary>
        private static bool CouldCreateCircularDependency(
            Type implementationType,
            Type dependencyType,
            IServiceCollection services
        )
        {
            // This is a simplified check - real circular dependency detection is complex
            if (dependencyType == implementationType)
                return true;

            // Check if the dependency is implemented by a type that depends on our implementation
            var dependencyImplementations = services
                .Where(s => s.ServiceType == dependencyType && s.ImplementationType != null)
                .Select(s => s.ImplementationType!)
                .ToList();

            foreach (var depImpl in dependencyImplementations)
            {
                var constructors = depImpl.GetConstructors();
                var primaryConstructor = constructors
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (primaryConstructor == null)
                    continue;

                var transitiveDeps = primaryConstructor
                    .GetParameters()
                    .Select(p => p.ParameterType)
                    .ToList();

                // Look for services that our implementation provides
                var ourServices = services
                    .Where(s => s.ImplementationType == implementationType)
                    .Select(s => s.ServiceType)
                    .ToList();

                if (transitiveDeps.Any(td => ourServices.Contains(td)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Results of DI configuration validation.
    /// </summary>
    public class ValidationResults
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public bool HasErrors => Errors.Any();
        public bool HasWarnings => Warnings.Any();
        public bool IsValid => !HasErrors;

        public void AddError(string error) => Errors.Add(error);

        public void AddWarning(string warning) => Warnings.Add(warning);

        public override string ToString()
        {
            var result = $"Validation Results - Errors: {Errors.Count}, Warnings: {Warnings.Count}";

            if (HasErrors)
            {
                result += $"\nErrors:\n  - {string.Join("\n  - ", Errors)}";
            }

            if (HasWarnings)
            {
                result += $"\nWarnings:\n  - {string.Join("\n  - ", Warnings)}";
            }

            return result;
        }
    }
}
