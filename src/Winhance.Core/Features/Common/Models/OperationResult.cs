using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public class OperationResult<T>
    {
        public bool Success { get; set; }
        public bool IsCancelled { get; set; }
        public T? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, string>? ErrorDetails { get; set; }
        public bool RequiresConfirmation { get; set; }
        public ConfirmationRequest? ConfirmationRequest { get; set; }

        public static OperationResult<T> CreateSuccess(T result)
        {
            return new OperationResult<T>
            {
                Success = true,
                Result = result
            };
        }

        public static OperationResult<T> CreateFailure(string errorMessage)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        public static OperationResult<T> CreateFailure(Exception exception)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = exception.Message,
                Exception = exception
            };
        }

        public static OperationResult<T> CreateFailure(string errorMessage, Exception exception)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }

        public bool Succeeded()
        {
            return Success;
        }

        public static OperationResult<T> Succeeded(string message)
        {
            return new OperationResult<T>
            {
                Success = true,
                ErrorMessage = message
            };
        }

        public static OperationResult<T> Succeeded(T result)
        {
            return new OperationResult<T>
            {
                Success = true,
                Result = result
            };
        }

        public bool Failed()
        {
            return !Success;
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

        public static OperationResult<T> Failed(string message, T result)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = message,
                Result = result
            };
        }

        public static OperationResult<T> CreateConfirmationRequired(ConfirmationRequest confirmationRequest)
        {
            return new OperationResult<T>
            {
                RequiresConfirmation = true,
                ConfirmationRequest = confirmationRequest
            };
        }

        public static OperationResult<T> Cancelled(string message = "Operation was cancelled")
        {
            return new OperationResult<T>
            {
                Success = false,
                IsCancelled = true,
                ErrorMessage = message
            };
        }
    }

    public class OperationResult
    {
        public bool Success { get; set; }
        public bool IsCancelled { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, string>? ErrorDetails { get; set; }
        public bool RequiresConfirmation { get; set; }
        public ConfirmationRequest? ConfirmationRequest { get; set; }

        public static OperationResult CreateSuccess()
        {
            return new OperationResult
            {
                Success = true
            };
        }

        public static OperationResult CreateFailure(string errorMessage)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        public static OperationResult CreateFailure(Exception exception)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = exception.Message,
                Exception = exception
            };
        }

        public static OperationResult CreateFailure(string errorMessage, Exception exception)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }

        public bool Succeeded()
        {
            return Success;
        }

        public static OperationResult Succeeded(string message)
        {
            return new OperationResult
            {
                Success = true,
                ErrorMessage = message
            };
        }

        public bool Failed()
        {
            return !Success;
        }

        public static OperationResult Failed(string message)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = message
            };
        }

        public static OperationResult Failed(string message, Exception exception)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = message,
                Exception = exception
            };
        }

        public static OperationResult CreateConfirmationRequired(ConfirmationRequest confirmationRequest)
        {
            return new OperationResult
            {
                RequiresConfirmation = true,
                ConfirmationRequest = confirmationRequest
            };
        }

        public static OperationResult Cancelled(string message = "Operation was cancelled")
        {
            return new OperationResult
            {
                Success = false,
                IsCancelled = true,
                ErrorMessage = message
            };
        }
    }
}