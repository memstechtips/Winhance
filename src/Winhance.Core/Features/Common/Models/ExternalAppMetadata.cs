namespace Winhance.Core.Features.Common.Models;

public sealed record ExternalAppMetadata
{
    public string? DownloadUrl { get; init; }
    public string? FallbackDownloadUrl { get; init; }
    public string? DownloadUrlArm64 { get; init; }
    public string? DownloadUrlX64 { get; init; }
    public string? DownloadUrlX86 { get; init; }
    public bool IsGitHubRelease { get; init; }
    public string? AssetPattern { get; init; }
    public bool RequiresDirectDownload { get; init; }

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
