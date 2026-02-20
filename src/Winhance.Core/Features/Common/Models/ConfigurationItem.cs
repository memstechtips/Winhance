using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    public class ConfigurationItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSelected { get; set; }

        public InputType InputType { get; set; } = InputType.Toggle;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AppxPackageName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? WinGetPackageId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? CapabilityName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? OptionalFeatureName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[]? SubPackages { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? SelectedIndex { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? CustomStateValues { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object>? PowerSettings { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? PowerPlanGuid { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? PowerPlanName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("SelectedValue is only used for backward compatibility during migration. Use SelectedIndex instead.")]
        public string? SelectedValue { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("CustomProperties is only used for backward compatibility during migration. Use specific properties instead.")]
        public Dictionary<string, object>? CustomProperties { get; set; }
    }
}
