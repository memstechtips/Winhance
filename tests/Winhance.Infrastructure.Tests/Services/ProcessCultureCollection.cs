using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// LocalizationService.SetLanguage changes the UI culture for the whole process, and TimeRegionLanguageService reads
// display names in it: run in parallel, a German name lands in an English assertion.
[CollectionDefinition(Name)]
public sealed class ProcessCultureCollection
{
    public const string Name = "process culture";
}
