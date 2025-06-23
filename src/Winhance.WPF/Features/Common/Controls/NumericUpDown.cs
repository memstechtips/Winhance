using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// A custom control that allows users to input numeric values with up/down buttons.
    /// </summary>
    public class NumericUpDown : Control
    {
        static NumericUpDown()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDown), 
                new FrameworkPropertyMetadata(typeof(NumericUpDown)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                "Value", 
                typeof(int), 
                typeof(NumericUpDown),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                "Minimum", 
                typeof(int), 
                typeof(NumericUpDown),
                new PropertyMetadata(0, OnMinimumChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                "Maximum", 
                typeof(int), 
                typeof(NumericUpDown),
                new PropertyMetadata(100, OnMaximumChanged));

        public static readonly DependencyProperty IncrementProperty =
            DependencyProperty.Register(
                "Increment", 
                typeof(int), 
                typeof(NumericUpDown),
                new PropertyMetadata(1));

        public static readonly DependencyProperty UnitsProperty =
            DependencyProperty.Register(
                "Units", 
                typeof(string), 
                typeof(NumericUpDown),
                new PropertyMetadata(string.Empty));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the current value.
        /// </summary>
        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Gets or sets the minimum allowed value.
        /// </summary>
        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum allowed value.
        /// </summary>
        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>
        /// Gets or sets the increment value when using the up/down buttons.
        /// </summary>
        public int Increment
        {
            get => (int)GetValue(IncrementProperty);
            set => SetValue(IncrementProperty, value);
        }

        /// <summary>
        /// Gets or sets the units text to display (e.g., "Minutes", "%").
        /// </summary>
        public string Units
        {
            get => (string)GetValue(UnitsProperty);
            set => SetValue(UnitsProperty, value);
        }

        #endregion

        #region Commands

        public static readonly RoutedCommand IncreaseCommand = new RoutedCommand("Increase", typeof(NumericUpDown));
        public static readonly RoutedCommand DecreaseCommand = new RoutedCommand("Decrease", typeof(NumericUpDown));

        #endregion

        #region Constructor

        public NumericUpDown()
        {
            CommandBindings.Add(new CommandBinding(IncreaseCommand, OnIncrease));
            CommandBindings.Add(new CommandBinding(DecreaseCommand, OnDecrease));
        }

        #endregion

        #region Command Handlers

        private void OnIncrease(object sender, ExecutedRoutedEventArgs e)
        {
            Value = Math.Min(Value + Increment, Maximum);
            e.Handled = true;
        }

        private void OnDecrease(object sender, ExecutedRoutedEventArgs e)
        {
            Value = Math.Max(Value - Increment, Minimum);
            e.Handled = true;
        }

        #endregion

        #region Property Changed Handlers

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericUpDown control)
            {
                // Ensure value is within range
                int newValue = (int)e.NewValue;
                if (newValue < control.Minimum)
                {
                    control.Value = control.Minimum;
                }
                else if (newValue > control.Maximum)
                {
                    control.Value = control.Maximum;
                }
            }
        }

        private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericUpDown control)
            {
                // Ensure minimum is less than or equal to maximum
                if (control.Minimum > control.Maximum)
                {
                    control.Maximum = control.Minimum;
                }

                // Ensure current value is not less than minimum
                if (control.Value < control.Minimum)
                {
                    control.Value = control.Minimum;
                }
            }
        }

        private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericUpDown control)
            {
                // Ensure maximum is greater than or equal to minimum
                if (control.Maximum < control.Minimum)
                {
                    control.Minimum = control.Maximum;
                }

                // Ensure current value is not greater than maximum
                if (control.Value > control.Maximum)
                {
                    control.Value = control.Maximum;
                }
            }
        }

        #endregion
    }
}
