using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SettingControlHandler
    {
        private readonly IWindowsRegistryService _registryService;
        private readonly IComboBoxResolver _comboBoxResolver;
        private readonly ILogService _logService;

        public SettingControlHandler(
            IWindowsRegistryService registryService,
            IComboBoxResolver comboBoxResolver,
            ILogService logService
        )
        {
            _registryService =
                registryService ?? throw new ArgumentNullException(nameof(registryService));
            _comboBoxResolver =
                comboBoxResolver ?? throw new ArgumentNullException(nameof(comboBoxResolver));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task ApplyBinaryToggleAsync(SettingDefinition setting, bool enable)
        {
            if (setting.RegistrySettings?.Count > 0)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    _registryService.ApplySetting(registrySetting, enable);
                }
            }
        }

        public async Task ApplyComboBoxIndexAsync(SettingDefinition setting, int comboBoxIndex)
        {
            if (_comboBoxResolver.CanResolve(setting))
            {
                await _comboBoxResolver.ApplyIndexAsync(setting, comboBoxIndex);
            }
            else
            {
                throw new InvalidOperationException(
                    $"ComboBox setting '{setting.Id}' cannot be resolved"
                );
            }
        }

        public async Task ApplyNumericUpDownAsync(SettingDefinition setting, object value)
        {
            var numericValue = value switch
            {
                int intVal => intVal,
                double doubleVal => (int)doubleVal,
                float floatVal => (int)floatVal,
                string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                _ => throw new ArgumentException(
                    $"Cannot convert '{value}' to numeric value for setting '{setting.Id}'"
                ),
            };

            if (setting.RegistrySettings?.Count > 0)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    _registryService.SetValue(
                        registrySetting.KeyPath,
                        registrySetting.ValueName,
                        numericValue,
                        Microsoft.Win32.RegistryValueKind.DWord
                    );
                }
            }
        }

        public async Task<bool> GetSettingStatusAsync(
            string settingId,
            IEnumerable<SettingDefinition> settings
        )
        {
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting?.RegistrySettings?.Count > 0)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    if (_registryService.IsSettingApplied(registrySetting))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<object?> GetSettingValueAsync(
            string settingId,
            IEnumerable<SettingDefinition> settings
        )
        {
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
                return null;

            if (
                setting.InputType == SettingInputType.Selection
                && _comboBoxResolver.CanResolve(setting)
            )
            {
                return await _comboBoxResolver.ResolveCurrentIndexAsync(setting);
            }

            if (setting.RegistrySettings?.Count > 0)
            {
                return _registryService.GetValue(
                    setting.RegistrySettings[0].KeyPath,
                    setting.RegistrySettings[0].ValueName
                );
            }

            return null;
        }
    }
}
