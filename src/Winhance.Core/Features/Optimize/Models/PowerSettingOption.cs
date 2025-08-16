namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a single option for a power setting ComboBox.
    /// This class provides proper property binding for WPF ComboBox controls.
    /// </summary>
    public class PowerSettingOption
    {
        /// <summary>
        /// The display name shown in the ComboBox.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The power setting value to be applied when this option is selected.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Returns the Name for display purposes.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Equality comparison based on Value.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is PowerSettingOption other)
            {
                return Value == other.Value;
            }
            return false;
        }

        /// <summary>
        /// Hash code based on Value.
        /// </summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}