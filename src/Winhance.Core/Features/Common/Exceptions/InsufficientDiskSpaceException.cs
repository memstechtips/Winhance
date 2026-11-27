namespace Winhance.Core.Features.Common.Exceptions;

public class InsufficientDiskSpaceException : Exception
{
    public string DriveName { get; } = string.Empty;
    public double RequiredGB { get; }
    public double AvailableGB { get; }
    public string OperationName { get; } = string.Empty;

    public InsufficientDiskSpaceException() { }
    public InsufficientDiskSpaceException(string message) : base(message) { }
    public InsufficientDiskSpaceException(string message, Exception innerException) : base(message, innerException) { }

    public InsufficientDiskSpaceException(
        string driveName,
        double requiredGB,
        double availableGB,
        string operationName)
        : base($"Insufficient disk space on {driveName} for {operationName}. " +
               $"Required: {requiredGB:F2} GB, Available: {availableGB:F2} GB")
    {
        DriveName = driveName;
        RequiredGB = requiredGB;
        AvailableGB = availableGB;
        OperationName = operationName;
    }
}
