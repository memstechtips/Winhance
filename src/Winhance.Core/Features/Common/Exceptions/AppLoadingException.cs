using System;

namespace Winhance.Core.Models.Exceptions
{
    public enum AppLoadingErrorCode
    {
        Unknown,
        CacheFailure,
        Timeout,
        PackageManagerError,
        InvalidConfiguration,
        NetworkError
    }

    public class AppLoadingException : Exception
    {
        public AppLoadingErrorCode ErrorCode { get; }
        public Exception? OriginalException { get; }

        public AppLoadingException(AppLoadingErrorCode errorCode, string message,
            Exception? originalException = null)
            : base(message, originalException)
        {
            ErrorCode = errorCode;
            OriginalException = originalException;
        }
    }

    public class PackageManagerException : Exception
    {
        public string PackageId { get; }
        public string Operation { get; }
        public Exception? OriginalException { get; }

        public PackageManagerException(string packageId, string operation, string message,
            Exception? originalException = null)
            : base($"Package {operation} failed for {packageId}: {message}", originalException)
        {
            PackageId = packageId;
            Operation = operation;
            OriginalException = originalException;
        }
    }
}
