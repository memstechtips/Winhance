using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public class UnifiedConfigurationFile
    {
        public string Version { get; set; } = "2.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ConfigSection WindowsApps { get; set; } = new ConfigSection();
        public ConfigSection ExternalApps { get; set; } = new ConfigSection();
        public FeatureGroupSection Customize { get; set; } = new FeatureGroupSection();
        public FeatureGroupSection Optimize { get; set; } = new FeatureGroupSection();
    }

    public class FeatureGroupSection
    {
        public bool IsIncluded { get; set; } = false;
        public IReadOnlyDictionary<string, ConfigSection> Features { get; set; } = new Dictionary<string, ConfigSection>();
    }

    public class ConfigSection
    {
        public bool IsIncluded { get; set; } = false;
        public IReadOnlyList<ConfigurationItem> Items { get; set; } = new List<ConfigurationItem>();
    }
}