using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code or internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate" />.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Find the cancel button in the template and hook up the event handler
            if (GetTemplateChild("PART_CancelButton") is Button cancelButton)
            {
                cancelButton.Click -= CancelButton_Click;
                cancelButton.Click += CancelButton_Click;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // First try to execute the command if it exists
            if (CancelCommand != null && CancelCommand.CanExecute(null))
            {
                CancelCommand.Execute(null);
                return;
            }

            // If command execution fails, use a more direct approach
            CancelCurrentTaskDirectly();
        }

        /// <summary>
        /// Directly cancels the current task by finding the TaskProgressService instance.
        /// </summary>
        private void CancelCurrentTaskDirectly()
        {
            try
            {
                // Get the application's main window
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    return;
                }

                // Get the DataContext of the main window (should be the MainViewModel)
                var mainViewModel = mainWindow.DataContext;
                if (mainViewModel == null)
                {
                    return;
                }

                // Use reflection to get the _progressService field
                var type = mainViewModel.GetType();
                var field = type.GetField("_progressService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    var progressService = field.GetValue(mainViewModel) as Winhance.Core.Features.Common.Interfaces.ITaskProgressService;
                    if (progressService != null)
                    {
                        progressService.CancelCurrentTask();

                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
            }
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

        /// <summary>
        /// Gets or sets the command to execute when the cancel button is clicked.
        /// </summary>
        public ICommand CancelCommand
        {
            get { return (ICommand)GetValue(CancelCommandProperty); }
            set { SetValue(CancelCommandProperty, value); }
        }

        /// <summary>
        /// Identifies the CancelCommand dependency property.
        /// </summary>
        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(ProgressIndicator),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets whether to show the cancel button instead of progress text.
        /// </summary>
        public bool ShowCancelButton
        {
            get { return (bool)GetValue(ShowCancelButtonProperty); }
            set { SetValue(ShowCancelButtonProperty, value); }
        }

        /// <summary>
        /// Identifies the ShowCancelButton dependency property.
        /// </summary>
        public static readonly DependencyProperty ShowCancelButtonProperty =
            DependencyProperty.Register(nameof(ShowCancelButton), typeof(bool), typeof(ProgressIndicator),
                new PropertyMetadata(false));

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
            if (IsIndeterminate || ShowCancelButton)
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
