using System.Collections.Generic;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    public class OperationResultAggregator
    {
        public int SuccessCount { get; private set; }
        public int TotalCount { get; private set; }
        public List<string> SuccessItems { get; } = new();
        public List<string> FailedItems { get; } = new();

        public void Add(string itemName, bool success, string? errorMessage = null)
        {
            TotalCount++;
            if (success)
            {
                SuccessCount++;
                SuccessItems.Add(itemName);
            }
            else
            {
                FailedItems.Add($"{itemName}: {errorMessage ?? "Unknown error"}");
            }
        }
    }
}