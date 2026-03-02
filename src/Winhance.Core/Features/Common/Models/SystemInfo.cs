namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Diagnostic system information collected for log headers.
/// </summary>
public record SystemInfo
{
    public string AppVersion { get; init; } = "Unknown";
    public string OperatingSystem { get; init; } = "Unknown";
    public string Architecture { get; init; } = "Unknown";
    public string DeviceType { get; init; } = "Unknown";
    public string Cpu { get; init; } = "Unknown";
    public string Ram { get; init; } = "Unknown";
    public string Gpu { get; init; } = "Unknown";
    public string DotNetRuntime { get; init; } = "Unknown";
    public string Elevation { get; init; } = "Unknown";
    public string FirmwareType { get; init; } = "Unknown";
    public string SecureBoot { get; init; } = "Unknown";
    public string Tpm { get; init; } = "Unknown";
    public string DomainJoined { get; init; } = "Unknown";
}
