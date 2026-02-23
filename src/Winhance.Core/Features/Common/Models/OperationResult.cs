using System;

namespace Winhance.Core.Features.Common.Models
{
    public class OperationResult<T>
    {
        public bool Success { get; set; }
        public T? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public bool RequiresConfirmation { get; set; }

        public static OperationResult<T> Succeeded(T result)
        {
            return new OperationResult<T>
            {
                Success = true,
                Result = result
            };
        }

        public static OperationResult<T> Failed(string message)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = message
            };
        }

        public static OperationResult<T> Failed(string message, Exception exception)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = message,
                Exception = exception
            };
        }

        public static OperationResult<T> Cancelled(string message = "Operation was cancelled")
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}