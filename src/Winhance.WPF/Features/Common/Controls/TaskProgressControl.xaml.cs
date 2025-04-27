using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.Models;

[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "Winhance.WPF.Features.Common.Controls")]
namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// Interaction logic for TaskProgressControl.xaml
    /// </summary>
    public partial class TaskProgressControl : UserControl
    {
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
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(TaskProgressControl), 
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
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(TaskProgressControl), 
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
            DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(TaskProgressControl), 
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether the control is visible.
        /// </summary>
        public new bool IsVisible
        {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }

        /// <summary>
        /// Identifies the IsVisible dependency property.
        /// </summary>
        public new static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(TaskProgressControl), 
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
            DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(TaskProgressControl), 
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets whether the details are expanded.
        /// </summary>
        public bool AreDetailsExpanded
        {
            get { return (bool)GetValue(AreDetailsExpandedProperty); }
            set { SetValue(AreDetailsExpandedProperty, value); }
        }

        /// <summary>
        /// Identifies the AreDetailsExpanded dependency property.
        /// </summary>
        public static readonly DependencyProperty AreDetailsExpandedProperty =
            DependencyProperty.Register(nameof(AreDetailsExpanded), typeof(bool), typeof(TaskProgressControl), 
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether there are log messages.
        /// </summary>
        public bool HasLogMessages
        {
            get { return (bool)GetValue(HasLogMessagesProperty); }
            private set { SetValue(HasLogMessagesProperty, value); }
        }

        /// <summary>
        /// Identifies the HasLogMessages dependency property.
        /// </summary>
        public static readonly DependencyProperty HasLogMessagesProperty =
            DependencyProperty.Register(nameof(HasLogMessages), typeof(bool), typeof(TaskProgressControl), 
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the log messages.
        /// </summary>
        public ObservableCollection<LogMessageViewModel> LogMessages
        {
            get { return (ObservableCollection<LogMessageViewModel>)GetValue(LogMessagesProperty); }
            private set { SetValue(LogMessagesProperty, value); }
        }

        /// <summary>
        /// Identifies the LogMessages dependency property.
        /// </summary>
        public static readonly DependencyProperty LogMessagesProperty =
            DependencyProperty.Register(nameof(LogMessages), typeof(ObservableCollection<LogMessageViewModel>), 
                typeof(TaskProgressControl), new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets whether the operation can be cancelled.
        /// </summary>
        public bool CanCancel
        {
            get { return (bool)GetValue(CanCancelProperty); }
            set { SetValue(CanCancelProperty, value); }
        }

        /// <summary>
        /// Identifies the CanCancel dependency property.
        /// </summary>
        public static readonly DependencyProperty CanCancelProperty =
            DependencyProperty.Register(nameof(CanCancel), typeof(bool), typeof(TaskProgressControl), 
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether a task is running.
        /// </summary>
        public bool IsTaskRunning
        {
            get { return (bool)GetValue(IsTaskRunningProperty); }
            set { SetValue(IsTaskRunningProperty, value); }
        }

        /// <summary>
        /// Identifies the IsTaskRunning dependency property.
        /// </summary>
        public static readonly DependencyProperty IsTaskRunningProperty =
            DependencyProperty.Register(nameof(IsTaskRunning), typeof(bool), typeof(TaskProgressControl), 
                new PropertyMetadata(false));

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
            DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(TaskProgressControl), 
                new PropertyMetadata(null));

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskProgressControl"/> class.
        /// </summary>
        public TaskProgressControl()
        {
            LogMessages = new ObservableCollection<LogMessageViewModel>();
            InitializeComponent();
            UpdateProgressText();
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TaskProgressControl control)
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

        /// <summary>
        /// Adds a log message to the control.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="level">The log level.</param>
        public void AddLogMessage(string message, LogLevel level)
        {
            if (string.IsNullOrEmpty(message)) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Add(new LogMessageViewModel
                {
                    Message = message,
                    Level = level,
                    Timestamp = DateTime.Now
                });

                HasLogMessages = LogMessages.Count > 0;

                // Auto-expand details on error or warning
                if (level == LogLevel.Error || level == LogLevel.Warning)
                {
                    AreDetailsExpanded = true;
                }
            });
        }

        /// <summary>
        /// Clears all log messages.
        /// </summary>
        public void ClearLogMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Clear();
                HasLogMessages = false;
            });
        }
    }
}
