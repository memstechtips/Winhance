using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// xUnit runs each test class in parallel by default, which let DefaultConfigGeneratorTests rewrite a shipped
// .winhance config while DefaultConfigConformanceTests was parsing it - a JsonException that reads like a catalog regression.
[CollectionDefinition(Name)]
public sealed class RepoFileWritersCollection
{
    public const string Name = "repo file writers";
}
