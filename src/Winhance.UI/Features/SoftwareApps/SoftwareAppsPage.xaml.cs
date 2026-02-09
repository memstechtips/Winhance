using System.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.Features.SoftwareApps.Views;

namespace Winhance.UI.Features.SoftwareApps;

public sealed partial class SoftwareAppsPage : Page
{
    public SoftwareAppsViewModel ViewModel { get; }

    public SoftwareAppsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SoftwareAppsViewModel>();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateTabBadges();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoftwareAppsViewModel.WindowsAppsSelectedCount) ||
            e.PropertyName == nameof(SoftwareAppsViewModel.ExternalAppsSelectedCount) ||
            e.PropertyName == nameof(SoftwareAppsViewModel.IsInReviewMode))
        {
            DispatcherQueue.TryEnqueue(UpdateTabBadges);
        }
    }

    private void UpdateTabBadges()
    {
        bool showBadges = ViewModel.IsInReviewMode;
        WindowsAppsTabBadge.Visibility = showBadges && ViewModel.WindowsAppsSelectedCount > 0
            ? Visibility.Visible : Visibility.Collapsed;
        ExternalAppsTabBadge.Visibility = showBadges && ViewModel.ExternalAppsSelectedCount > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // During startup, MainWindow handles initialization with the loading overlay visible
        if (e.Parameter as string == "startup")
            return;

        await ViewModel.InitializeAsync();
    }

    private void WindowsAppsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var checkBox = FindSelectAllCheckBox(WindowsAppsDataGrid);
        if (checkBox != null)
        {
            checkBox.Checked += (s, _) => SetAllItemsSelected(ViewModel.WindowsAppsViewModel.ItemsView, true);
            checkBox.Unchecked += (s, _) => SetAllItemsSelected(ViewModel.WindowsAppsViewModel.ItemsView, false);
        }
    }

    private void ExternalAppsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var checkBox = FindSelectAllCheckBox(ExternalAppsDataGrid);
        if (checkBox != null)
        {
            checkBox.Checked += (s, _) => SetAllItemsSelected(ViewModel.ExternalAppsViewModel.ItemsView, true);
            checkBox.Unchecked += (s, _) => SetAllItemsSelected(ViewModel.ExternalAppsViewModel.ItemsView, false);
        }
    }

    private void SetAllItemsSelected(AdvancedCollectionView itemsView, bool isSelected)
    {
        foreach (var item in itemsView)
        {
            if (item is AppItemViewModel appItem)
                appItem.IsSelected = isSelected;
        }
    }

    private static CheckBox? FindSelectAllCheckBox(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is CheckBox cb && cb.Tag?.ToString() == "SelectAll")
                return cb;
            var found = FindSelectAllCheckBox(child);
            if (found != null)
                return found;
        }
        return null;
    }

    private void TableViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsCardViewMode)
        {
            // Already in table mode — keep it checked
            TableViewToggle.IsChecked = true;
            return;
        }
        ViewModel.IsCardViewMode = false;
    }

    private void CardViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsCardViewMode)
        {
            // Already in card mode — keep it checked
            CardViewToggle.IsChecked = true;
            return;
        }
        ViewModel.IsCardViewMode = true;
    }

    private void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        string? sortProperty = e.Column.Tag?.ToString();
        if (string.IsNullOrEmpty(sortProperty))
            return;

        AdvancedCollectionView? collectionView = null;
        if (dataGrid == WindowsAppsDataGrid)
            collectionView = ViewModel.WindowsAppsViewModel.ItemsView;
        else if (dataGrid == ExternalAppsDataGrid)
            collectionView = ViewModel.ExternalAppsViewModel.ItemsView;

        if (collectionView == null)
            return;

        var newDirection = e.Column.SortDirection switch
        {
            null => DataGridSortDirection.Ascending,
            DataGridSortDirection.Ascending => DataGridSortDirection.Descending,
            _ => DataGridSortDirection.Ascending
        };

        foreach (var column in dataGrid.Columns)
        {
            column.SortDirection = null;
        }

        collectionView.SortDescriptions.Clear();
        collectionView.SortDescriptions.Add(new SortDescription(
            sortProperty,
            newDirection == DataGridSortDirection.Ascending
                ? SortDirection.Ascending
                : SortDirection.Descending));

        e.Column.SortDirection = newDirection;
    }

    private async void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var localization = App.Services.GetRequiredService<Core.Features.Common.Interfaces.ILocalizationService>();

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            CloseButtonText = localization.GetString("Help_CloseHelp"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (ViewModel.IsWindowsAppsTabSelected)
        {
            dialog.Title = localization.GetString("Help_WindowsApps_Title");
            var scheduledTaskService = App.Services.GetRequiredService<Core.Features.Common.Interfaces.IScheduledTaskService>();
            var logService = App.Services.GetRequiredService<Core.Features.Common.Interfaces.ILogService>();

            var vm = new RemovalStatusContainerViewModel(scheduledTaskService, logService);
            var content = new WindowsAppsHelpContent(localization);
            content.DataContext = vm;
            dialog.Content = content;

            _ = vm.RefreshAllStatusesAsync();
            await dialog.ShowAsync();
            vm.Dispose();
        }
        else
        {
            dialog.Title = localization.GetString("Help_ExternalApps_Title");
            dialog.Content = new ExternalAppsHelpContent(localization);
            await dialog.ShowAsync();
        }
    }
}
