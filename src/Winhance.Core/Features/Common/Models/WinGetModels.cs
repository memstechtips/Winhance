namespace Winhance.Core.Features.Common.Models
{
    public class WinGetOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
    }

    public class WinGetProgress
    {
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsCancelled { get; set; }
    }

    public class InstallationProgress
    {
        public string Status { get; set; } = string.Empty;
        public string LastLine { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsError { get; set; }
        public bool IsConnectivityIssue { get; set; }
    }
}
