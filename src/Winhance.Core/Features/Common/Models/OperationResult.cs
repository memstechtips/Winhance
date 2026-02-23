using System;

namespace Winhance.Core.Features.Common.Models
{
    public class OperationResult<T>
    {
        public bool Success { get; }
        public T? Result { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }
        public bool RequiresConfirmation { get; }

        private OperationResult(
            bool success,
            T? result = default,
            string? errorMessage = null,
            Exception? exception = null,
            bool requiresConfirmation = false)
        {
            Success = success;
            Result = result;
            ErrorMessage = errorMessage;
            Exception = exception;
            RequiresConfirmation = requiresConfirmation;
        }

        public static OperationResult<T> Succeeded(T result)
        {
            return new OperationResult<T>(success: true, result: result);
        }

        public static OperationResult<T> Failed(string message)
        {
            return new OperationResult<T>(success: false, errorMessage: message);
        }

        public static OperationResult<T> Failed(string message, Exception exception)
        {
            return new OperationResult<T>(success: false, errorMessage: message, exception: exception);
        }

        public static OperationResult<T> Cancelled(string message = "Operation was cancelled")
        {
            return new OperationResult<T>(success: false, errorMessage: message);
        }

        public static OperationResult<T> ConfirmationRequired(string message)
        {
            return new OperationResult<T>(success: false, errorMessage: message, requiresConfirmation: true);
        }
    }
}
