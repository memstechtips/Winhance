using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service responsible for assigning event delegates to SettingUIItem controls.
    /// Follows SRP by handling only delegate assignment logic.
    /// </summary>
    public class SettingsDelegateAssignmentService : ISettingsDelegateAssignmentService
    {
        private readonly ILogService _logService;

        public SettingsDelegateAssignmentService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public void AssignDelegates(
            SettingUIItem item, 
            Func<string, bool, Task> onToggleChange, 
            Func<string, object?, Task> onValueChange)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (onToggleChange == null) throw new ArgumentNullException(nameof(onToggleChange));
            if (onValueChange == null) throw new ArgumentNullException(nameof(onValueChange));

            // Clear existing delegates to prevent memory leaks
            ClearDelegates(item);

            // Assign delegates based on control type
            switch (item.ControlType)
            {
                case ControlType.BinaryToggle:
                    item.OnSettingChanged = async (isEnabled) =>
                    {
                        await onToggleChange(item.Id, isEnabled);
                    };
                    _logService.Log(LogLevel.Debug, $"Binary toggle delegate assigned for setting '{item.Id}'");
                    break;

                case ControlType.ComboBox:
                case ControlType.NumericUpDown:
                case ControlType.Slider:
                    item.OnSettingValueChanged = async (value) =>
                    {
                        _logService.Log(LogLevel.Debug, $"OnSettingValueChanged delegate invoked for '{item.Id}', value: {value}");
                        await onValueChange(item.Id, value);
                    };
                    _logService.Log(LogLevel.Debug, $"{item.ControlType} delegate assigned for setting '{item.Id}'");
                    break;

                default:
                    _logService.Log(LogLevel.Warning, $"Unknown control type '{item.ControlType}' for setting '{item.Id}'. No delegates assigned.");
                    break;
            }
        }

        public void ClearDelegates(SettingUIItem item)
        {
            if (item == null) return;

            item.OnSettingChanged = null;
            item.OnSettingValueChanged = null;
        }
    }
}
