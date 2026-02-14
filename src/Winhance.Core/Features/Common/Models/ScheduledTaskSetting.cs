namespace Winhance.Core.Features.Common.Models
{
    public class ScheduledTaskSetting
    {
        public string Id { get; set; } = string.Empty;
        public string TaskPath { get; set; } = string.Empty;
        public bool? RecommendedState { get; set; }
    }
}
