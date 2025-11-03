using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Winhance.WPF.Features.Common.Controls
{
    public partial class ContentLoadingOverlay : UserControl, INotifyPropertyChanged
    {
        private DispatcherTimer _themeCheckTimer;

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                nameof(IsVisible),
                typeof(bool),
                typeof(ContentLoadingOverlay),
                new PropertyMetadata(false, OnIsVisibleChanged));

        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register(
                nameof(StatusMessage),
                typeof(string),
                typeof(ContentLoadingOverlay),
                new PropertyMetadata("Loading..."));

        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            set => SetValue(StatusMessageProperty, value);
        }

        public bool IsThemeDark
        {
            get
            {
                if (Application.Current.Resources.Contains("IsDarkTheme"))
                {
                    return (bool)Application.Current.Resources["IsDarkTheme"];
                }
                return true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ContentLoadingOverlay()
        {
            InitializeComponent();

            _themeCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _themeCheckTimer.Tick += (s, e) => OnPropertyChanged(nameof(IsThemeDark));
        }

        private static void OnIsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ContentLoadingOverlay overlay)
            {
                if ((bool)e.NewValue)
                {
                    overlay._themeCheckTimer?.Start();
                    overlay.OnPropertyChanged(nameof(overlay.IsThemeDark));
                }
                else
                {
                    overlay._themeCheckTimer?.Stop();
                }
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
