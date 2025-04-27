using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the result of an operation, including success status, error details, and a result value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    public class OperationResult<T>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the result value of the operation.
        /// </summary>
        public T? Result { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception that occurred during the operation, if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets additional error details.
        /// </summary>
        public Dictionary<string, string>? ErrorDetails { get; set; }

        /// <summary>
        /// Creates a successful operation result with the specified result value.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <returns>A successful operation result.</returns>
        public static OperationResult<T> CreateSuccess(T result)
        {
            return new OperationResult<T>
            {
                Success = true,
                Result = result
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> CreateFailure(string errorMessage)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified exception.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> CreateFailure(Exception exception)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = exception.Message,
                Exception = exception
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified error message and exception.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> CreateFailure(string errorMessage, Exception exception)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }

        /// <summary>
        /// Checks if the operation was successful.
        /// </summary>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public bool Succeeded()
        {
            return Success;
        }

        /// <summary>
        /// Creates a successful operation result with the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A successful operation result.</returns>
        public static OperationResult<T> Succeeded(string message)
        {
            return new OperationResult<T>
            {
                Success = true,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// Creates a successful operation result with the specified result value.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <returns>A successful operation result.</returns>
        public static OperationResult<T> Succeeded(T result)
        {
            return new OperationResult<T>
            {
                Success = true,
                Result = result
            };
        }

        /// <summary>
        /// Checks if the operation failed.
        /// </summary>
        /// <returns>True if the operation failed; otherwise, false.</returns>
        public bool Failed()
        {
            return !Success;
        }

        /// <summary>
        /// Creates a failed operation result with the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> Failed(string message)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified message and exception.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult<T> Failed(string message, Exception exception)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorMessage = message,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// Represents the result of an operation, including success status and error details.
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception that occurred during the operation, if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets additional error details.
        /// </summary>
        public Dictionary<string, string>? ErrorDetails { get; set; }

        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        /// <returns>A successful operation result.</returns>
        public static OperationResult CreateSuccess()
        {
            return new OperationResult
            {
                Success = true
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult CreateFailure(string errorMessage)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified exception.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult CreateFailure(Exception exception)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = exception.Message,
                Exception = exception
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified error message and exception.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult CreateFailure(string errorMessage, Exception exception)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }

        /// <summary>
        /// Checks if the operation was successful.
        /// </summary>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public bool Succeeded()
        {
            return Success;
        }

        /// <summary>
        /// Creates a successful operation result with the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A successful operation result.</returns>
        public static OperationResult Succeeded(string message)
        {
            return new OperationResult
            {
                Success = true,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// Checks if the operation failed.
        /// </summary>
        /// <returns>True if the operation failed; otherwise, false.</returns>
        public bool Failed()
        {
            return !Success;
        }

        /// <summary>
        /// Creates a failed operation result with the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult Failed(string message)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// Creates a failed operation result with the specified message and exception.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>A failed operation result.</returns>
        public static OperationResult Failed(string message, Exception exception)
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = message,
                Exception = exception
            };
        }
    }
}