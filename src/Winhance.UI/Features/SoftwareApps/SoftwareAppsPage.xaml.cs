using System.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.Helpers;
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
        ApplyTextScaling();
        ViewModel = App.Services.GetRequiredService<SoftwareAppsViewModel>();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateTabBadges();

        // DataGrid column headers can't use {x:Bind}/{Binding} (CommunityToolkit
        // columns live outside the page's binding tree), so set them from the
        // localized ViewModel strings here and re-apply on language change below.
        LocalizeColumnHeaders();

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

    // WinUI 3 auto-scales TextBlock font sizes but fixed cell widths in Page.Resources stay put; the compact row's
    // 320dp cell grows with text scale so fewer columns fit and rows wrap into a longer list (issue #668).
    // Mutating Page.Resources right after InitializeComponent() works because {StaticResource} inside an
    // ItemsPanelTemplate resolves at template instantiation, and the compact panels haven't materialised yet.
    // Card-view geometry is deliberately NOT scaled: the AppCardTemplate's inner Grid uses MinHeight and the
    // UniformWrapPanel treats ItemHeight as a floor, so cards grow taller without dropping columns.
    private void ApplyTextScaling()
    {
        if (!TextScaleHelper.IsScaled) return;
        var f = TextScaleHelper.Factor;
        ScaleResource("CompactItemWidth", f);
        // CompactNameMaxWidth scales with the cell so larger text gets
        // proportionally more horizontal room before CharacterEllipsis fires.
        ScaleResource("CompactNameMaxWidth", f);
    }

    private void ScaleResource(string key, double factor)
    {
        if (Resources.TryGetValue(key, out var value) && value is double d)
            Resources[key] = d * factor;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoftwareAppsViewModel.WindowsAppsSelectedCount) ||
            e.PropertyName == nameof(SoftwareAppsViewModel.ExternalAppsSelectedCount) ||
            e.PropertyName == nameof(SoftwareAppsViewModel.IsInReviewMode))
        {
            DispatcherQueue.TryEnqueue(UpdateTabBadges);
        }
        else if (e.PropertyName == nameof(SoftwareAppsViewModel.ColumnHeaderName))
        {
            // OnLanguageChanged raises all ColumnHeader* together; one is enough to re-apply.
            DispatcherQueue.TryEnqueue(LocalizeColumnHeaders);
        }
    }

    // Matches each column by its Tag (the same value the sort handler uses); untagged selection/spacer columns are left alone.
    private void LocalizeColumnHeaders()
    {
        ApplyColumnHeaders(WindowsAppsDataGrid);
        ApplyColumnHeaders(ExternalAppsDataGrid);
    }

    private void ApplyColumnHeaders(DataGrid? grid)
    {
        if (grid is null)
            return;

        foreach (var column in grid.Columns)
        {
            string? header = (column.Tag as string) switch
            {
                "Name" => ViewModel.ColumnHeaderName,
                "Description" => ViewModel.ColumnHeaderDescription,
                "ItemTypeDescription" => ViewModel.ColumnHeaderType,
                "IsInstalled" => ViewModel.ColumnHeaderStatus,
                "CanBeReinstalled" => ViewModel.ColumnHeaderInstallable,
                "CategoryDisplayName" => ViewModel.ColumnHeaderGroup,
                _ => null
            };

            if (header is not null)
                column.Header = header;
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

    private void SortInstalledFirst_Click(object sender, RoutedEventArgs e)
        => ViewModel.SortMode = AppSortMode.NameAscInstalledFirst;

    private void SortNameAsc_Click(object sender, RoutedEventArgs e)
        => ViewModel.SortMode = AppSortMode.NameAsc;

    private void SortNameDesc_Click(object sender, RoutedEventArgs e)
        => ViewModel.SortMode = AppSortMode.NameDesc;

    // MaxWidth = cols x CardItemWidth + (cols-1) x ColumnSpacing, so header, Select-all checkboxes and grid share
    // the same extents; extra space becomes symmetric outer margin (the Store / Settings pattern).
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

    // External Apps nests each category's grid inside an Expander, so the grid only gets the clamp width minus the
    // Expander's horizontal content chrome (border + padding); this accounts for that. If category grids show a
    // right-hand gap, lower it; if a column wraps away too early, raise it. Windows Apps has no Expander and passes 0.
    private const double CompactExternalExpanderChrome = 34.0;

    private void WindowsAppsCompactScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCompactContentMaxWidth(WindowsAppsCompactScrollViewer, WindowsAppsCompactContentStack, 0.0);

    private void ExternalAppsCompactScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCompactContentMaxWidth(ExternalAppsCompactScrollViewer, ExternalAppsCompactContentStack, CompactExternalExpanderChrome);

    private void UpdateCompactContentMaxWidth(ScrollViewer scrollViewer, StackPanel contentStack, double expanderChrome)
    {
        // Resource key lives in SoftwareAppsPage.xaml's Page.Resources; fall back
        // to the same default if it is renamed so the page still lays out.
        double itemWidth = Resources["CompactItemWidth"] is double iw ? iw : 320.0;
        double spacing = 8.0; // matches ColumnSpacing on the compact UniformWrapPanels

        double available = scrollViewer.ViewportWidth - expanderChrome;
        if (available <= 0)
            return;

        int cols = System.Math.Max(1,
            (int)System.Math.Floor((available + spacing) / (itemWidth + spacing)));
        double contentWidth = cols * itemWidth + System.Math.Max(0, cols - 1) * spacing + expanderChrome;
        contentStack.MaxWidth = contentWidth;
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
