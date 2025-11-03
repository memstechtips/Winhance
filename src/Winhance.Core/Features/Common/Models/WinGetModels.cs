namespace Winhance.Core.Features.Common.Models
{
    public class WinGetOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string PackageId { get; set; }
    }

    public class WinGetProgress
    {
        public string Status { get; set; }
        public string Details { get; set; }
        public bool IsActive { get; set; }
        public bool IsCancelled { get; set; }
    }

    public class InstallationProgress
    {
        public string Status { get; set; }
        public string LastLine { get; set; }
        public bool IsActive { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsError { get; set; }
        public bool IsConnectivityIssue { get; set; }
    }
}
