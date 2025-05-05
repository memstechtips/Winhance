using System;

namespace Winhance.Core.Features.Common.Enums
{
    /// <summary>
    /// Defines the type of control to use for a setting.
    /// </summary>
    public enum ControlType
    {
        /// <summary>
        /// A binary toggle (on/off) control.
        /// </summary>
        BinaryToggle,

        /// <summary>
        /// A three-state slider control.
        /// </summary>
        ThreeStateSlider,

        /// <summary>
        /// A combo box control for selecting from a list of options.
        /// </summary>
        ComboBox,

        /// <summary>
        /// A custom control.
        /// </summary>
        Custom,

        /// <summary>
        /// A slider control.
        /// </summary>
        Slider,

        /// <summary>
        /// A dropdown control.
        /// </summary>
        Dropdown,

        /// <summary>
        /// A color picker control.
        /// </summary>
        ColorPicker
    }
}