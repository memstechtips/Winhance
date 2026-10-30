using System.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.SoftwareApps;

public sealed partial class SoftwareAppsPage : Page
{
    private readonly HashSet<CheckBox> _wiredHeaderBoxes = [];

    public SoftwareAppsViewModel ViewModel { get; }

    public SoftwareAppsPage()
    {
        this.InitializeComponent();
        ApplyTextScaling();
        SizeChanged += (_, _) => FitActionBar();
        // Review mode swaps the buttons for toggles, and a language switch or a new selection changes the labels, all
        // without a resize.
        ActionBar.SizeChanged += (_, _) => FitActionBar();
        ViewModel = App.Services.GetRequiredService<SoftwareAppsViewModel>();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        // handledEventsToo: a row's check box or the grid may already have marked the right-tap handled.
        ExternalAppsContent.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler(ExternalApp_RightTapped), true);
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
        var f = ReadTextScaleFactor();
        if (f <= 1.0 + 0.001) return;
        ScaleResource("CompactItemWidth", f);
        // CompactNameMaxWidth scales with the cell so larger text gets
        // proportionally more horizontal room before CharacterEllipsis fires.
        ScaleResource("CompactNameMaxWidth", f);
    }

    private static double ReadTextScaleFactor()
    {
        try
        {
            return new Windows.UI.ViewManagement.UISettings().TextScaleFactor;
        }
        catch
        {
            // UISettings can throw in some elevated/limited contexts; no scaling beats a broken layout.
            return 1.0;
        }
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
                "VersionSortKey" => ViewModel.ColumnHeaderVersion,
                "HasUpdate" => ViewModel.ColumnHeaderUpdate,
                "InstalledOn" => ViewModel.ColumnHeaderInstalledOn,
                "SizeBytes" => ViewModel.ColumnHeaderSize,
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
        var viewModel = ViewModel.WindowsAppsViewModel;
        WireSelectAllHeaderWhenReady(WindowsAppsDataGrid, checkBox => SyncSelectAllHeader(checkBox, viewModel, viewModel.ItemsView));
    }

    private void ExternalAppsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel.ExternalAppsViewModel;
        WireSelectAllHeaderWhenReady(ExternalAppsDataGrid, checkBox => SyncSelectAllHeader(checkBox, viewModel, viewModel.ItemsView));
    }

    // The header box acts on the rows the search leaves visible, unlike the Select All box above the grid, which acts
    // on every app. It sits in a column-header template x:Bind cannot reach, so its state is kept by hand.
    private static void SyncSelectAllHeader(CheckBox checkBox, INotifyPropertyChanged viewModel, AdvancedCollectionView itemsView)
    {
        List<AppItemViewModel> VisibleRows() => itemsView.OfType<AppItemViewModel>().Where(r => r.IsSelectable).ToList();
        void Refresh()
        {
            var rows = VisibleRows();
            checkBox.IsChecked = rows.Count > 0 && rows.TrueForAll(r => r.IsSelected);
        }

        Refresh();
        itemsView.VectorChanged += (_, _) => Refresh();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WindowsAppsViewModel.IsAllSelected))
                Refresh();
        };
        checkBox.Click += (_, _) =>
        {
            var rows = VisibleRows();
            var select = !rows.TrueForAll(r => r.IsSelected);
            foreach (var row in rows)
                row.IsSelected = select;
            Refresh();
        };
    }

    // A grid on a tab that has not been shown yet loads before its header template exists, so the box is looked for
    // again on each layout pass until it does.
    private void WireSelectAllHeaderWhenReady(DataGrid grid, Action<CheckBox> wire)
    {
        if (TryWire())
            return;

        EventHandler<object>? retry = null;
        retry = (_, _) =>
        {
            if (TryWire())
                grid.LayoutUpdated -= retry;
        };
        grid.LayoutUpdated += retry;

        bool TryWire()
        {
            var checkBox = FindSelectAllCheckBox(grid);
            if (checkBox == null)
                return false;

            if (_wiredHeaderBoxes.Add(checkBox))
                wire(checkBox);
            return true;
        }
    }

    // Arrow keys focus the row, not its check box, so Space ticks the selected rows the way a click on the box does.
    private void AppsDataGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space || e.OriginalSource is CheckBox || sender is not DataGrid grid)
            return;

        var rows = grid.SelectedItems.OfType<AppItemViewModel>().Where(r => r.IsSelectable).ToList();
        if (rows.Count == 0)
            return;

        var select = !rows.TrueForAll(r => r.IsSelected);
        foreach (var row in rows)
            row.IsSelected = select;
        e.Handled = true;
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

    private void ViewCard_Click(object sender, RoutedEventArgs e)
        => ViewModel.ViewMode = SoftwareAppsViewMode.Card;

    private void ViewTable_Click(object sender, RoutedEventArgs e)
        => ViewModel.ViewMode = SoftwareAppsViewMode.Table;

    private void ViewCompact_Click(object sender, RoutedEventArgs e)
        => ViewModel.ViewMode = SoftwareAppsViewMode.Compact;

    private void FilterAll_Click(object sender, RoutedEventArgs e)
        => ViewModel.SetFilter(AppFilter.All);

    private void FilterInstalled_Click(object sender, RoutedEventArgs e)
        => ViewModel.SetFilter(AppFilter.Installed);

    private void FilterUpdates_Click(object sender, RoutedEventArgs e)
        => ViewModel.SetFilter(AppFilter.UpdatesAvailable);

    // One menu for the table, card and compact views. It hangs off the External tab's content, not the card template,
    // because Windows Apps shares that template.
    private void ExternalApp_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement { DataContext: AppItemViewModel app } target)
            return;

        // CommandParameter goes first: the item asks CanExecute when Command is set, and greys out for a Store-only app.
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem
        {
            Text = ViewModel.InstallOptionsMenuText,
            CommandParameter = app,
            Command = ViewModel.ExternalAppsViewModel.EditInstallOptionsCommand
        });
        menu.ShowAt(target, e.GetPosition(target));
        e.Handled = true;
    }

    // Refresh, Help, View and Install options give up their labels first; Install and Remove keep theirs longest.
    private void FitActionBar()
    {
        if (ActionBar.Parent is not FrameworkElement row || row.ActualWidth <= 0)
            return;

        UIElement[] secondary = [RefreshLabel, HelpLabel, ViewMenuLabel, InstallOptionsLabel];
        UIElement[] primary = [InstallLabel, RemoveLabel, InstallToggleLabel, RemoveToggleLabel];
        FitToWidth.Apply(
            row.ActualWidth,
            () =>
            {
                ActionBar.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                return ActionBar.DesiredSize.Width;
            },
            reset: () => FitToWidth.SetVisible(true, [.. secondary, .. primary]),
            () => FitToWidth.SetVisible(false, secondary),
            () => FitToWidth.SetVisible(false, primary));
    }

    private void SortInstalledFirst_Click(object sender, RoutedEventArgs e)
        => ViewModel.SortMode = AppSortMode.NameAscInstalledFirst;

    private void SortNameAsc_Click(object sender, RoutedEventArgs e)
        => ViewModel.SortMode = AppSortMode.NameAsc;

    private void SortNameDesc_Click(object sender, RoutedEventArgs e)
        => ViewModel.SortMode = AppSortMode.NameDesc;

    // MaxWidth = cols x cell + (cols-1) x ColumnSpacing, so header, Select-all checkboxes and grid share the same
    // extents; extra space becomes symmetric outer margin (the Store / Settings pattern).
    private void WindowsAppsCardScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCardContentMaxWidth(WindowsAppsCardScrollViewer, WindowsAppsCardContentStack);

    private void ExternalAppsCardScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCardContentMaxWidth(ExternalAppsCardScrollViewer, ExternalAppsCardContentStack);

    private void UpdateCardContentMaxWidth(ScrollViewer scrollViewer, StackPanel contentStack)
    {
        // Resource keys live in SoftwareAppsPage.xaml's Page.Resources; fall back to
        // the same defaults if anything's renamed so the page still lays out.
        double cardWidth = Resources["CardItemWidth"] is double cw ? cw : 420.0;
        double minCardWidth = Resources["CardMinItemWidth"] is double mw ? mw : 320.0;
        double spacing = 16.0; // matches ColumnSpacing/RowSpacing on UniformWrapPanel

        double available = scrollViewer.ViewportWidth;
        if (available <= 0)
            return;

        var (cols, cellWidth) = UniformWrapPanel.Fit(available, cardWidth, minCardWidth, spacing, int.MaxValue);
        contentStack.MaxWidth = cols * cellWidth + System.Math.Max(0, cols - 1) * spacing;
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

        var newDirection = e.Column.SortDirection switch
        {
            null => DataGridSortDirection.Ascending,
            DataGridSortDirection.Ascending => DataGridSortDirection.Descending,
            _ => DataGridSortDirection.Ascending
        };

        AdvancedCollectionView? collectionView = null;
        if (dataGrid == WindowsAppsDataGrid)
            collectionView = ViewModel.WindowsAppsViewModel.ItemsView;
        else if (dataGrid == ExternalAppsDataGrid)
            collectionView = ViewModel.ExternalAppsViewModel.ItemsView;

        if (collectionView == null)
            return;

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
