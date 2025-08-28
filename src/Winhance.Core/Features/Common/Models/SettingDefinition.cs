using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Core.Features.Common.Models
{
    public record SettingDefinition : ISettingItem
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string GroupName { get; init; }
        public SettingInputType InputType { get; init; } = SettingInputType.Toggle;
        public string? Icon { get; init; }
        public bool RequiresConfirmation { get; init; } = false;
        public string? ConfirmationTitle { get; init; }
        public string? ConfirmationMessage { get; init; }
        public string? ConfirmationCheckboxText { get; init; }
        public string? ActionCommand { get; init; }
        public bool IsWindows11Only { get; init; }
        public bool IsWindows10Only { get; init; }
        public int? MinimumBuildNumber { get; init; }
        public int? MaximumBuildNumber { get; init; }
        public List<(int MinBuild, int MaxBuild)> SupportedBuildRanges { get; init; } =
            new List<(int, int)>();
        public List<RegistrySetting> RegistrySettings { get; init; } = new List<RegistrySetting>();
        public List<CommandSetting> CommandSettings { get; init; } = new List<CommandSetting>();
        public List<SettingDependency> Dependencies { get; init; } = new List<SettingDependency>();
        public Dictionary<string, object> CustomProperties { get; init; } =
            new Dictionary<string, object>();
    }
}
