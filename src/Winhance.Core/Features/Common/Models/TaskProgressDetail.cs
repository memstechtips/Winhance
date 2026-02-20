using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    public class TaskProgressDetail
    {
        public double? Progress { get; set; }
        public string StatusText { get; set; }
        public string DetailedMessage { get; set; }
        public LogLevel LogLevel { get; set; } = LogLevel.Info;
        public bool IsIndeterminate { get; set; }
        public Dictionary<string, string> AdditionalInfo { get; set; } = new Dictionary<string, string>();
        public string TerminalOutput { get; set; }
        public bool IsActive { get; set; }
        public int QueueTotal { get; set; }
        public int QueueCurrent { get; set; }
        public string? QueueNextItemName { get; set; }
        public int ScriptSlotIndex { get; set; } = -1;  // -1 = single-mode (existing behavior)
        public int ScriptSlotCount { get; set; }         // 0 = single-mode
        public bool IsCompletion { get; set; }           // true = intentional task completion signal
        public bool IsProgressIndicator { get; set; }   // true = replaces previous progress line (like \r in a terminal)
    }
}