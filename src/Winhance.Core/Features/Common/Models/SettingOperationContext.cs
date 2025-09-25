using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public class SettingOperationContext
    {
        public string SettingId { get; set; } = string.Empty;
        public string? CommandString { get; set; }
        public bool Enable { get; set; }
        public object? Value { get; set; }
        public bool ApplyRecommendedSettings { get; set; }
        public bool ApplyWallpaper { get; set; }
        public Dictionary<string, int?>? RegistryValues { get; set; }
        public Dictionary<string, object> AdditionalParameters { get; set; } = new();
        public bool IsActionOperation => !string.IsNullOrEmpty(CommandString);
    }
}