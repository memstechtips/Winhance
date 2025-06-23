using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents an advanced power setting that can be modified through the UI.
    /// </summary>
    public class AdvancedPowerSetting : INotifyPropertyChanged
    {
        private int _acValue;
        private int _dcValue;
        private bool _isUpdatingFromCode;

        /// <summary>
        /// Gets or sets the definition of the power setting.
        /// </summary>
        public PowerSettingDefinition Definition { get; set; }

        /// <summary>
        /// Gets or sets the AC value of the power setting.
        /// </summary>
        public int AcValue
        {
            get => _acValue;
            set
            {
                if (_acValue != value)
                {
                    _acValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the DC value of the power setting.
        /// </summary>
        public int DcValue
        {
            get => _dcValue;
            set
            {
                if (_dcValue != value)
                {
                    _dcValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the setting is currently being updated from code.
        /// </summary>
        public bool IsUpdatingFromCode
        {
            get => _isUpdatingFromCode;
            set
            {
                if (_isUpdatingFromCode != value)
                {
                    _isUpdatingFromCode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the display name of the setting.
        /// </summary>
        public string DisplayName => Definition?.DisplayName ?? string.Empty;

        /// <summary>
        /// Gets or sets the description of the setting.
        /// </summary>
        public string Description => Definition?.Description ?? string.Empty;

        /// <summary>
        /// Gets or sets the subgroup GUID of the setting.
        /// </summary>
        public string SubgroupGuid => Definition?.SubgroupGuid ?? string.Empty;

        /// <summary>
        /// Gets or sets the setting GUID.
        /// </summary>
        public string SettingGuid => Definition?.Guid ?? string.Empty;

        /// <summary>
        /// Gets or sets the alias of the setting.
        /// </summary>
        public string Alias => Definition?.Alias ?? string.Empty;

        /// <summary>
        /// Gets or sets the type of the setting.
        /// </summary>
        public PowerSettingType SettingType => Definition?.SettingType ?? PowerSettingType.Numeric;

        /// <summary>
        /// Gets or sets the possible values for enum settings.
        /// </summary>
        public List<PowerSettingValue> PossibleValues => Definition?.PossibleValues ?? new List<PowerSettingValue>();

        /// <summary>
        /// Gets or sets the units for numeric settings.
        /// </summary>
        public string Units => Definition?.Units ?? string.Empty;

        /// <summary>
        /// Gets or sets the minimum value for numeric settings.
        /// </summary>
        public int MinValue => Definition?.MinValue ?? 0;

        /// <summary>
        /// Gets or sets the maximum value for numeric settings.
        /// </summary>
        public int MaxValue => Definition?.MaxValue ?? 100;

        /// <summary>
        /// Gets or sets the increment for numeric settings.
        /// </summary>
        public int Increment => Definition?.Increment ?? 1;

        /// <summary>
        /// Gets a value indicating whether this setting should use predefined time intervals.
        /// </summary>
        public bool UseTimeIntervals => Definition?.UseTimeIntervals ?? false;

        /// <summary>
        /// Gets the predefined time values for settings that use time intervals.
        /// </summary>
        public List<PowerSettingTimeValue> TimeValues => Definition?.TimeValues ?? new List<PowerSettingTimeValue>();

        /// <summary>
        /// Gets the friendly name for the current AC value.
        /// </summary>
        public string AcValueFriendlyName
        {
            get
            {
                if (Definition?.SettingType == PowerSettingType.Enum && Definition.PossibleValues != null)
                {
                    var value = Definition.PossibleValues.Find(v => v.Index == AcValue);
                    return value?.FriendlyName ?? AcValue.ToString();
                }
                return AcValue.ToString();
            }
        }

        /// <summary>
        /// Gets the friendly name for the current DC value.
        /// </summary>
        public string DcValueFriendlyName
        {
            get
            {
                if (Definition?.SettingType == PowerSettingType.Enum && Definition.PossibleValues != null)
                {
                    var value = Definition.PossibleValues.Find(v => v.Index == DcValue);
                    return value?.FriendlyName ?? DcValue.ToString();
                }
                return DcValue.ToString();
            }
        }

        /// <summary>
        /// Creates a PowerSettingApplyValue from this setting.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID to apply the setting to.</param>
        /// <returns>A PowerSettingApplyValue that can be applied to a power plan.</returns>
        public PowerSettingApplyValue CreateApplyValue(string powerPlanGuid)
        {
            return new PowerSettingApplyValue
            {
                SettingGuid = SettingGuid,
                SubgroupGuid = SubgroupGuid,
                PowerPlanGuid = powerPlanGuid,
                AcValue = AcValue,
                DcValue = DcValue,
                DisplayName = DisplayName,
                Alias = Alias
            };
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a group of advanced power settings.
    /// </summary>
    public class AdvancedPowerSettingGroup : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets or sets the subgroup definition.
        /// </summary>
        public PowerSettingSubgroup Subgroup { get; set; }

        /// <summary>
        /// Gets or sets the display name of the group.
        /// </summary>
        public string DisplayName => Subgroup?.DisplayName ?? string.Empty;

        /// <summary>
        /// Gets or sets the GUID of the group.
        /// </summary>
        public string Guid => Subgroup?.Guid ?? string.Empty;

        /// <summary>
        /// Gets or sets the alias of the group.
        /// </summary>
        public string Alias => Subgroup?.Alias ?? string.Empty;

        /// <summary>
        /// Gets or sets the settings in the group.
        /// </summary>
        public List<AdvancedPowerSetting> Settings { get; set; } = new List<AdvancedPowerSetting>();

        /// <summary>
        /// Gets or sets a value indicating whether the group is expanded in the UI.
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
