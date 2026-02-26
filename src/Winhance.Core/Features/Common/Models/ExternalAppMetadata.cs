namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Typed metadata for external application download definitions.
/// </summary>
public record ExternalAppMetadata
{
    public string? DownloadUrl { get; init; }
    public string? DownloadUrlArm64 { get; init; }
    public string? DownloadUrlX64 { get; init; }
    public string? DownloadUrlX86 { get; init; }
    public bool IsGitHubRelease { get; init; }
    public string? AssetPattern { get; init; }
    public bool RequiresDirectDownload { get; init; }

    /// <summary>
    /// Gets the architecture-specific download URL, falling back to the generic URL.
    /// </summary>
    public string? GetDownloadUrlForArchitecture(string architecture)
    {
        return architecture switch
        {
            "arm64" => DownloadUrlArm64 ?? DownloadUrl,
            "x64" => DownloadUrlX64 ?? DownloadUrl,
            "x86" => DownloadUrlX86 ?? DownloadUrl,
            _ => DownloadUrl
        };
    }
}
