using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// Serialises the tests that WRITE working-tree files against the ones that READ them. xUnit runs each
/// test class as its own collection in parallel by default, which let DefaultConfigGeneratorTests
/// rewrite a shipped .winhance config while DefaultConfigConformanceTests was parsing it - surfacing as
/// a JsonException that reads like a catalog regression.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RepoFileWritersCollection
{
    public const string Name = "repo file writers";
}
