using System;
using System.Windows;
using System.Windows.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class ConfigImportOptionsDialog : Window
    {
        public ImportOption SelectedOption { get; private set; }

        public ConfigImportOptionsDialog()
        {
            InitializeComponent();
            SelectedOption = ImportOption.None;

            this.Loaded += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = true;
                }
            };

            this.Closed += (s, e) =>
            {
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.IsDialogOverlayVisible = false;
                }
            };
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ImportOwnConfig_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = ImportOption.ImportOwn;
            this.DialogResult = true;
            this.Close();
        }

        private void ImportRecommendedConfig_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = ImportOption.ImportRecommended;
            this.DialogResult = true;
            this.Close();
        }
    }
}
