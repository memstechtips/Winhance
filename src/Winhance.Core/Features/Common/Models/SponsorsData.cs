using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Represents a single sponsor entry from the sponsors JSON document.
/// </summary>
public sealed record SponsorEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("contact")]
    public string? Contact { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("logo")]
    public string? Logo { get; init; }

    [JsonPropertyName("since")]
    public string? Since { get; init; }

    [JsonPropertyName("example")]
    public bool Example { get; init; }

    [JsonPropertyName("until")]
    public string? Until { get; init; }
}

/// <summary>
/// Represents a community supporter entry from the sponsors JSON document.
/// </summary>
public sealed record SupporterEntry
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("since")]
    public string? Since { get; init; }
}

/// <summary>
/// Top-level sponsors JSON document.
/// </summary>
public sealed record SponsorsDocument
{
    [JsonPropertyName("updated")]
    public string? Updated { get; init; }

    [JsonPropertyName("sponsors")]
    public List<SponsorEntry> Sponsors { get; init; } = new();

    [JsonPropertyName("supporters")]
    public List<SupporterEntry> Supporters { get; init; } = new();
}
