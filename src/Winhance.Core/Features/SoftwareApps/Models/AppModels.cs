using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.SoftwareApps.Models;

public enum AppInstallType
{
    Store,
    WinGet,
    DirectDownload,
    Custom
}

public enum TaskAction
{
    Apply,
    Test,
    Rollback
}

public record AppInstallConfig
{
    public required string FriendlyName { get; init; }
    public required AppInstallType InstallType { get; init; }
    public string? PackageId { get; init; }
    public string? DownloadUrl { get; init; }
    public string? CustomInstallHandler { get; init; }
    public IDictionary<string, string>? CustomProperties { get; init; }
    public bool IsInstalled { get; init; }
}

public record InstallResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
}

public class RegistryTestResult
{
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public object? ActualValue { get; set; }
    public object? ExpectedValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}