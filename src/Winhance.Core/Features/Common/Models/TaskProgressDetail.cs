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
    }
}