using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// A custom control that displays a progress indicator with status text.
    /// </summary>
    public class ProgressIndicator : Control
    {
        static ProgressIndicator()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ProgressIndicator), new FrameworkPropertyMetadata(typeof(ProgressIndicator)));
        }

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the progress value (0-100).
        /// </summary>
        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        /// <summary>
        /// Identifies the Progress dependency property.
        /// </summary>
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressIndicator), 
                new PropertyMetadata(0.0, OnProgressChanged));

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        public string StatusText
        {
            get { return (string)GetValue(StatusTextProperty); }
            set { SetValue(StatusTextProperty, value); }
        }

        /// <summary>
        /// Identifies the StatusText dependency property.
        /// </summary>
        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(ProgressIndicator), 
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate
        {
            get { return (bool)GetValue(IsIndeterminateProperty); }
            set { SetValue(IsIndeterminateProperty, value); }
        }

        /// <summary>
        /// Identifies the IsIndeterminate dependency property.
        /// </summary>
        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(ProgressIndicator), 
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether the control is active.
        /// </summary>
        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }

        /// <summary>
        /// Identifies the IsActive dependency property.
        /// </summary>
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ProgressIndicator), 
                new PropertyMetadata(false));

        /// <summary>
        /// Gets the progress text (e.g., "50%").
        /// </summary>
        public string ProgressText
        {
            get { return (string)GetValue(ProgressTextProperty); }
            private set { SetValue(ProgressTextProperty, value); }
        }

        /// <summary>
        /// Identifies the ProgressText dependency property.
        /// </summary>
        public static readonly DependencyProperty ProgressTextProperty =
            DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(ProgressIndicator), 
                new PropertyMetadata(string.Empty));

        #endregion

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressIndicator control)
            {
                control.UpdateProgressText();
            }
        }

        private void UpdateProgressText()
        {
            if (IsIndeterminate)
            {
                ProgressText = string.Empty;
            }
            else
            {
                ProgressText = $"{Progress:F0}%";
            }
        }
    }
}
