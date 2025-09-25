using System;

namespace Winhance.Core.Features.Common.Models
{

    public class CommandSetting
    {

        public string Id { get; set; } = string.Empty;

        public string EnabledCommand { get; set; } = string.Empty;

        public string DisabledCommand { get; set; } = string.Empty;

        public bool RequiresElevation { get; set; } = true;

    }
}
