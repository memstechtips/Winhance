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
        /// Gets or sets the app name being installed.
        /// </summary>
        public string AppName
        {
            get { return (string)GetValue(AppNameProperty); }
            set { SetValue(AppNameProperty, value); }
        }

        /// <summary>
        /// Identifies the AppName dependency property.
        /// </summary>
        public static readonly DependencyProperty AppNameProperty =
            DependencyProperty.Register(nameof(AppName), typeof(string), typeof(TaskProgressControl),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets the last terminal line.
        /// </summary>
        public string LastTerminalLine
        {
            get { return (string)GetValue(LastTerminalLineProperty); }
            set { SetValue(LastTerminalLineProperty, value); }
        }

        /// <summary>
        /// Identifies the LastTerminalLine dependency property.
        /// </summary>
        public static readonly DependencyProperty LastTerminalLineProperty =
            DependencyProperty.Register(nameof(LastTerminalLine), typeof(string), typeof(TaskProgressControl),
                new PropertyMetadata(string.Empty));

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
            InitializeComponent();
        }
    }
}
