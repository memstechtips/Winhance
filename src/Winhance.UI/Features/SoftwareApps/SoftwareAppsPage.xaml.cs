using System.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;

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

        // WinUI 3 InfoBar on a cached page does not re-evaluate its internal
        // ThemeResource bindings when the app theme changes.  Work around this
        // by closing and re-opening the visible banner on the next dispatcher
        // tick so the control rebuilds its visual tree with the new brushes.
        var themeService = App.Services.GetRequiredService<IThemeService>();
        themeService.ThemeChanged += (_, _) =>
        {
            if (!ViewModel.IsInReviewMode)
                return;

            WindowsAppsReviewBanner.IsOpen = false;
            ExternalAppsReviewBanner.IsOpen = false;

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                WindowsAppsReviewBanner.IsOpen = true;
                ExternalAppsReviewBanner.IsOpen = true;
            });
        };
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

    private void CardViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsCardView)
        {
            // Already in this mode — keep the toggle visually pressed.
            CardViewToggle.IsChecked = true;
            return;
        }
        ViewModel.ViewMode = SoftwareAppsViewMode.Card;
    }

    private void TableViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsTableView)
        {
            TableViewToggle.IsChecked = true;
            return;
        }
        ViewModel.ViewMode = SoftwareAppsViewMode.Table;
    }

    private void CompactViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsCompactView)
        {
            CompactViewToggle.IsChecked = true;
            return;
        }
        ViewModel.ViewMode = SoftwareAppsViewMode.Compact;
    }

    /// <summary>
    /// Centres the card-view content column horizontally by clamping the inner
    /// StackPanel's MaxWidth to exactly cols × CardItemWidth + (cols-1) × ColumnSpacing.
    /// Result: the section header, Select-all checkboxes, and the card grid all
    /// share the same horizontal extents — the leftmost card sits flush with the
    /// header instead of the cards floating in the middle of a left-aligned panel.
    /// Same pattern Microsoft Store / Settings use: extra space becomes symmetric
    /// outer margin, new columns appear at width breakpoints.
    /// </summary>
    private void WindowsAppsCardScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCardContentMaxWidth(WindowsAppsCardScrollViewer, WindowsAppsCardContentStack);

    private void ExternalAppsCardScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCardContentMaxWidth(ExternalAppsCardScrollViewer, ExternalAppsCardContentStack);

    private void UpdateCardContentMaxWidth(ScrollViewer scrollViewer, StackPanel contentStack)
    {
        // Resource keys live in SoftwareAppsPage.xaml's Page.Resources; fall back to
        // the same defaults if anything's renamed so the page still lays out.
        double cardWidth = Resources["CardItemWidth"] is double cw ? cw : 420.0;
        double spacing = 16.0; // matches ColumnSpacing/RowSpacing on UniformWrapPanel

        double available = scrollViewer.ViewportWidth;
        if (available <= 0)
            return;

        int cols = System.Math.Max(1,
            (int)System.Math.Floor((available + spacing) / (cardWidth + spacing)));
        double contentWidth = cols * cardWidth + System.Math.Max(0, cols - 1) * spacing;
        contentStack.MaxWidth = contentWidth;
    }

    private async void WebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to launch {url}: {ex.Message}");
            }
        }
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

}
