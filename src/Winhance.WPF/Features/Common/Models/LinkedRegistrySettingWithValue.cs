using System;
using System.ComponentModel;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Wrapper class for RegistrySetting that includes the current value.
    /// Used for displaying registry settings with their current values in tooltips.
    /// </summary>
    public class LinkedRegistrySettingWithValue : INotifyPropertyChanged
    {
        private object? _currentValue;
        
        /// <summary>
        /// Gets the underlying registry setting.
        /// </summary>
        public RegistrySetting Setting { get; }

        /// <summary>
        /// Gets or sets the current value of the registry setting.
        /// </summary>
        public object? CurrentValue 
        { 
            get => _currentValue;
            set
            {
                if (_currentValue != value)
                {
                    System.Diagnostics.Debug.WriteLine($"[LinkedRegistrySettingWithValue] CurrentValue changed from '{_currentValue}' to '{value}' for setting '{Setting.Name}'");
                    _currentValue = value;
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(DisplayValue));
                }
            }
        }

        /// <summary>
        /// Gets a user-friendly display value for the current registry value.
        /// Returns "Key doesn't exist" when the value is null, otherwise returns the actual value.
        /// </summary>
        public string DisplayValue
        {
            get
            {
                if (CurrentValue == null)
                {
                    return "Key doesn't exist";
                }
                return CurrentValue.ToString() ?? "Key doesn't exist";
            }
        }

        /// <summary>
        /// Creates a new instance of the LinkedRegistrySettingWithValue class.
        /// </summary>
        /// <param name="setting">The registry setting.</param>
        /// <param name="currentValue">The current value of the registry setting.</param>
        public LinkedRegistrySettingWithValue(RegistrySetting setting, object? currentValue)
        {
            Setting = setting ?? throw new ArgumentNullException(nameof(setting));
            _currentValue = currentValue; // Set backing field directly to avoid triggering PropertyChanged in constructor
        }
        
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
