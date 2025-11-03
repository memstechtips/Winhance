using System;

namespace Winhance.Core.Models.Exceptions
{
    public class InstallationException : Exception
    {
        public bool IsRecoverable { get; }
        public string ItemName { get; }

        public InstallationException(string itemName, string message,
            bool isRecoverable = false, Exception? inner = null)
            : base(message, inner)
        {
            ItemName = itemName;
            IsRecoverable = isRecoverable;
        }
    }

    public class InstallationCancelledException : InstallationException
    {
        public InstallationCancelledException(string itemName)
            : base(itemName, $"Installation cancelled for {itemName}", true) { }
    }

    public class DependencyInstallationException : InstallationException
    {
        public string DependencyName { get; }

        public DependencyInstallationException(string itemName, string dependencyName)
            : base(itemName, $"Dependency {dependencyName} failed to install for {itemName}", true)
        {
            DependencyName = dependencyName;
        }
    }

    public class ItemOperationException : InstallationException
    {
        public ItemOperationException(string itemName, string operation, string message,
            bool isRecoverable = false, Exception? inner = null)
            : base(itemName, $"{operation} failed for {itemName}: {message}", isRecoverable, inner) { }
    }

    public class RemovalException : ItemOperationException
    {
        public RemovalException(string itemName, string message,
            bool isRecoverable = false, Exception? inner = null)
            : base(itemName, "removal", message, isRecoverable, inner) { }
    }

    public class BatchOperationException : InstallationException
    {
        public int FailedCount { get; }
        public string OperationType { get; }

        public BatchOperationException(string operationType, int failedCount, int totalCount)
            : base("multiple items",
                  $"{operationType} failed for {failedCount} of {totalCount} items",
                  true)
        {
            OperationType = operationType;
            FailedCount = failedCount;
        }
    }
}
