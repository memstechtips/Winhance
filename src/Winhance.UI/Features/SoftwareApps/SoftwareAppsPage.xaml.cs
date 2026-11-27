using System.ComponentModel;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.SoftwareApps;

public sealed partial class SoftwareAppsPage : Page
{
    private readonly MemoryDiagnostics _memoryDiagnostics = App.Services.GetRequiredService<MemoryDiagnostics>();
    private readonly IDispatcherService _dispatcherService = App.Services.GetRequiredService<IDispatcherService>();
    private readonly ILogService _logService = App.Services.GetRequiredService<ILogService>();
    private readonly IThemeService _themeService = App.Services.GetRequiredService<IThemeService>();
    private readonly Dictionary<DataGrid, Action> _headerUnwires = [];
    private readonly Dictionary<string, (string Property, DataGridSortDirection Direction)> _columnSorts = [];
    private int _viewSwitch;
    private Task _listRestore = Task.CompletedTask;
    private double _textScale = 1.0;
    private int _windowsLineColumns;
    private int _externalLineColumns;
    private bool _wasAway;

    public SoftwareAppsViewModel ViewModel { get; }

    // Each view's x:Load is bound to this, so the generated code builds the named view, bindings included, and drops
    // the others.
    public static readonly DependencyProperty ShownViewProperty =
        DependencyProperty.Register(nameof(ShownView), typeof(string), typeof(SoftwareAppsPage), new PropertyMetadata(string.Empty));

    public string ShownView
    {
        get => (string)GetValue(ShownViewProperty);
        set => SetValue(ShownViewProperty, value);
    }

    public bool IsShown(string shownView, string name) => shownView == name;

    // Only the selected tab's panel exists. A bound x:Load, not FindName: an element FindName builds after the page's
    // bindings have started leaves its children's x:Load and x:Bind values unset.
    public static readonly DependencyProperty IsWindowsAppsPanelLoadedProperty =
        DependencyProperty.Register(nameof(IsWindowsAppsPanelLoaded), typeof(bool), typeof(SoftwareAppsPage), new PropertyMetadata(false));

    public bool IsWindowsAppsPanelLoaded
    {
        get => (bool)GetValue(IsWindowsAppsPanelLoadedProperty);
        set => SetValue(IsWindowsAppsPanelLoadedProperty, value);
    }

    public static readonly DependencyProperty IsExternalAppsPanelLoadedProperty =
        DependencyProperty.Register(nameof(IsExternalAppsPanelLoaded), typeof(bool), typeof(SoftwareAppsPage), new PropertyMetadata(false));

    public bool IsExternalAppsPanelLoaded
    {
        get => (bool)GetValue(IsExternalAppsPanelLoadedProperty);
        set => SetValue(IsExternalAppsPanelLoadedProperty, value);
    }

    public bool HasTabBadge(bool isInReviewMode, int selectedCount) => isInReviewMode && selectedCount > 0;

    public static readonly DependencyProperty WindowsSectionLinesProperty =
        DependencyProperty.Register(nameof(WindowsSectionLines), typeof(IReadOnlyList<AppSectionLine>), typeof(SoftwareAppsPage), new PropertyMetadata(null));

    public IReadOnlyList<AppSectionLine>? WindowsSectionLines
    {
        get => (IReadOnlyList<AppSectionLine>?)GetValue(WindowsSectionLinesProperty);
        set => SetValue(WindowsSectionLinesProperty, value);
    }

    public static readonly DependencyProperty ExternalSectionLinesProperty =
        DependencyProperty.Register(nameof(ExternalSectionLines), typeof(IReadOnlyList<AppSectionLine>), typeof(SoftwareAppsPage), new PropertyMetadata(null));

    public IReadOnlyList<AppSectionLine>? ExternalSectionLines
    {
        get => (IReadOnlyList<AppSectionLine>?)GetValue(ExternalSectionLinesProperty);
        set => SetValue(ExternalSectionLinesProperty, value);
    }

    public SoftwareAppsPage()
    {
        this.InitializeComponent();
        ApplyTextScaling();
        SizeChanged += OnSizeChanged;
        // Review mode swaps the buttons for toggles, and a language switch or a new selection changes the labels, all
        // without a resize.
        ActionBar.SizeChanged += OnSizeChanged;
        ViewModel = App.Services.GetRequiredService<SoftwareAppsViewModel>();
        IsWindowsAppsPanelLoaded = ViewModel.IsWindowsAppsTabSelected;
        IsExternalAppsPanelLoaded = !ViewModel.IsWindowsAppsTabSelected;
        ShownView = ActiveViewName();
    }

    private void OnThemeChanged(object? sender, WinhanceTheme theme) => ReopenReviewBanners();

    // WinUI 3 InfoBar on a cached page does not re-evaluate its internal ThemeResource bindings when the app theme
    // changes, so the shown banner is closed and opened again on the next tick to rebuild with the new brushes.
    private void ReopenReviewBanners()
    {
        if (!ViewModel.IsInReviewMode)
            return;

        WindowsAppsReviewBanner?.IsOpen = false;
        ExternalAppsReviewBanner?.IsOpen = false;

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            WindowsAppsReviewBanner?.IsOpen = true;
            ExternalAppsReviewBanner?.IsOpen = true;
        });
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => FitActionBar();

    private void OnWindowsAppsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowsAppsViewModel.Sections))
            _dispatcherService.Enqueue(SplitWindowsSections);
    }

    private void OnExternalAppsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExternalAppsViewModel.Categories))
            _dispatcherService.Enqueue(SplitExternalSections);
    }

    // WinUI 3 auto-scales TextBlock font sizes but fixed cell widths in Page.Resources stay put; the compact row's
    // 320dp cell grows with text scale so fewer columns fit and rows wrap into a longer list (issue #668).
    // Mutating Page.Resources right after InitializeComponent() works because {StaticResource} inside a template
    // resolves when the template is instantiated, and no row has been built yet.
    // Card widths are deliberately NOT scaled: the AppCardTemplate's inner Grid uses MinHeight and the card view
    // scales only its row height, so cards grow taller without dropping columns.
    private void ApplyTextScaling()
    {
        _textScale = ReadTextScaleFactor();
        if (_textScale <= 1.0 + 0.001) return;
        ScaleResource("CompactItemWidth", _textScale);
        // CompactNameMaxWidth scales with the cell so larger text gets
        // proportionally more horizontal room before CharacterEllipsis fires.
        ScaleResource("CompactNameMaxWidth", _textScale);
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
        if (e.PropertyName == nameof(SoftwareAppsViewModel.IsInReviewMode))
        {
            // The action bar builds its buttons or toggles for the new mode, so the labels are measured again after.
            _dispatcherService.Enqueue(FitActionBar);
        }
        else if (e.PropertyName == nameof(SoftwareAppsViewModel.ColumnHeaderName))
        {
            // OnLanguageChanged raises all ColumnHeader* together; one is enough to re-apply.
            _dispatcherService.Enqueue(LocalizeColumnHeaders);
        }
        else if ((e.PropertyName == nameof(SoftwareAppsViewModel.IsWindowsAppsTabSelected) && ViewModel.IsWindowsAppsTabSelected)
            || (e.PropertyName == nameof(SoftwareAppsViewModel.IsExternalAppsTabSelected) && ViewModel.IsExternalAppsTabSelected)
            || e.PropertyName == nameof(SoftwareAppsViewModel.ViewMode))
        {
            _dispatcherService.Enqueue(() => ShowSelectedViewAsync().FireAndForget(_logService));
        }
    }

    private string ActiveViewName() => (ViewModel.IsWindowsAppsTabSelected ? "WindowsApps" : "ExternalApps") + ViewModel.ViewMode switch
    {
        SoftwareAppsViewMode.Table => "TableView",
        SoftwareAppsViewMode.Compact => "CompactView",
        _ => "CardView"
    };

    // Only the shown view of the shown tab exists, so a tab or view switch builds one from nothing and would freeze the
    // old one on screen. The rocket goes up first, the build runs two frames later under it, and the view is revealed
    // once it has laid out. A view still loading its apps shows its own loader instead, because it builds nothing yet.
    private async Task ShowSelectedViewAsync()
    {
        var windows = ViewModel.IsWindowsAppsTabSelected;
        var wasShown = (windows ? WindowsAppsPanel : ExternalAppsPanel) is not null;
        IsWindowsAppsPanelLoaded = windows;
        IsExternalAppsPanelLoaded = !windows;
        var show = windows ? WindowsAppsPanel : ExternalAppsPanel;
        show ??= (Grid)FindName(windows ? nameof(WindowsAppsPanel) : nameof(ExternalAppsPanel));
        var name = ActiveViewName();
        var viewSwitch = ++_viewSwitch;

        var ready = ViewModel.IsWindowsAppsTabSelected
            ? ViewModel.WindowsAppsViewModel.IsContentReady
            : ViewModel.ExternalAppsViewModel.IsContentReady;
        var built = ShownView == name;
        if (!ready || (built && wasShown && show.Opacity == 1))
        {
            ShownView = name;
            show.Opacity = 1;
            TabLoader.IsActive = false;
            _memoryDiagnostics.Log($"apps {name}");
            return;
        }

        TabLoader.IsActive = true;
        show.Opacity = 0;
        await RenderFrames.NextAsync();
        await RenderFrames.NextAsync();
        if (viewSwitch != _viewSwitch)
            return;

        ShownView = name;
        await RenderFrames.NextAsync();
        await RenderFrames.NextAsync();
        if (viewSwitch != _viewSwitch)
            return;

        // The kept place is restored under the rocket, so the view does not open at the top and then jump.
        await _listRestore;
        if (viewSwitch != _viewSwitch)
            return;

        show.Opacity = 1;
        TabLoader.IsActive = false;
        _memoryDiagnostics.Log($"apps {name}");
        NativeHeaps.ReleaseDroppedMemoryLater(_dispatcherService, _logService);
    }

    // Matches each column by its Tag (the same value the sort handler uses); untagged selection/spacer columns are left alone.
    private void LocalizeColumnHeaders()
    {
        foreach (var grid in _headerUnwires.Keys)
            ApplyColumnHeaders(grid);
    }

    private void ApplyColumnHeaders(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            string? header = (column.Tag as string) switch
            {
                "Name" => ViewModel.ColumnHeaderName,
                "Description" => ViewModel.ColumnHeaderDescription,
                "ItemTypeDescription" => ViewModel.ColumnHeaderType,
                "IsInstalled" => ViewModel.ColumnHeaderStatus,
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

    // The view models and the theme service outlive this page, so it listens only while shown, and on the way back
    // redoes what each handler would have done while it was away.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.WindowsAppsViewModel.PropertyChanged += OnWindowsAppsPropertyChanged;
        ViewModel.ExternalAppsViewModel.PropertyChanged += OnExternalAppsPropertyChanged;
        _themeService.ThemeChanged += OnThemeChanged;
        if (_wasAway)
            _dispatcherService.Enqueue(CatchUp);
        if (ShownView.Length == 0)
            ShowSelectedViewAsync().FireAndForget(_logService);

        // During startup, MainWindow handles initialization with the loading overlay visible
        if (e.Parameter as string == "startup")
            return;

        ViewModel.InitializeAsync().FireAndForget(_logService);
    }

    // The frame keeps this page while another one shows, and with it every row of the shown view; the view goes when
    // the page does and is built again on the way back.
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.WindowsAppsViewModel.PropertyChanged -= OnWindowsAppsPropertyChanged;
        ViewModel.ExternalAppsViewModel.PropertyChanged -= OnExternalAppsPropertyChanged;
        _themeService.ThemeChanged -= OnThemeChanged;
        _wasAway = true;
        ++_viewSwitch;
        ShownView = string.Empty;
        foreach (var app in ViewModel.WindowsAppsViewModel.Items.Concat(ViewModel.ExternalAppsViewModel.Items))
            app.ReleaseIcon();
    }

    private void CatchUp()
    {
        FitActionBar();
        LocalizeColumnHeaders();
        SplitWindowsSections();
        SplitExternalSections();
        ReopenReviewBanners();
    }

    private void WindowsAppsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel.WindowsAppsViewModel;
        var grid = (DataGrid)sender;
        OnDataGridLoaded(grid, viewModel.ItemsView, checkBox => SyncSelectAllHeader(checkBox, viewModel));
        _listRestore = ScrollMemory.KeepAsync(grid.Name, grid);
        _listRestore.FireAndForget(_logService);
    }

    private void ExternalAppsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel.ExternalAppsViewModel;
        var grid = (DataGrid)sender;
        OnDataGridLoaded(grid, viewModel.ItemsView, checkBox => SyncSelectAllHeader(checkBox, viewModel));
        _listRestore = ScrollMemory.KeepAsync(grid.Name, grid);
        _listRestore.FireAndForget(_logService);
    }

    // Toolkit columns sit outside the page's binding tree, so their headers are set by hand, and the grid is rebuilt
    // each time its view is shown.
    private void OnDataGridLoaded(DataGrid grid, AdvancedCollectionView itemsView, Func<CheckBox, Action> wire)
    {
        grid.ItemsSource ??= itemsView;
        ApplyColumnHeaders(grid);
        RestoreSortArrow(grid, itemsView);
        WireSelectAllHeaderWhenReadyAsync(grid, wire).FireAndForget(_logService);
    }

    // The header box handlers sit on the view model, which outlives the grid. So does the collection view: the toolkit
    // grid subscribes to its change events and never lets go, so an unloaded grid stays alive with every row it built
    // (about 22 MB a visit) and keeps reacting to each list change until its source is cleared.
    private void AppsDataGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.IsLoaded)
            return;

        if (_headerUnwires.Remove(grid, out var unwire))
            unwire();
        grid.ItemsSource = null;
    }

    // The search template's bindings track the view models, which outlive the view, so a view leaving the tree drops
    // the template's content and with it those bindings.
    private void PackageSearch_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentControl { IsLoaded: false } search)
            search.Content = null;
    }

    private void PackageSearch_Loaded(object sender, RoutedEventArgs e) => ((ContentControl)sender).Content ??= ViewModel;

    // The arrow comes back only while the clicked column's sort is still the one applied; the Sort menu may have replaced it.
    private void RestoreSortArrow(DataGrid grid, AdvancedCollectionView itemsView)
    {
        if (!_columnSorts.TryGetValue(grid.Name, out var sort) || itemsView.SortDescriptions.Count != 1)
            return;

        var current = itemsView.SortDescriptions[0];
        var direction = current.Direction == SortDirection.Ascending ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;
        if (current.PropertyName != sort.Property || direction != sort.Direction)
            return;

        foreach (var column in grid.Columns)
            column.SortDirection = column.Tag as string == sort.Property ? sort.Direction : null;
    }

    // The header box is the Select All box above the grid, reached by hand because it sits in a column-header template
    // x:Bind cannot reach.
    private static Action SyncSelectAllHeader(CheckBox checkBox, ISelectAllSource viewModel)
    {
        void Refresh() => checkBox.IsChecked = viewModel.IsAllSelected;
        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ISelectAllSource.IsAllSelected))
                Refresh();
        }
        // The click has already flipped the box; it shows what the rows say even when the toggle changed none.
        void OnClick(object sender, RoutedEventArgs e)
        {
            viewModel.ToggleSelectAllCommand.Execute(null);
            Refresh();
        }

        Refresh();
        viewModel.PropertyChanged += OnPropertyChanged;
        checkBox.Click += OnClick;
        return () =>
        {
            viewModel.PropertyChanged -= OnPropertyChanged;
            checkBox.Click -= OnClick;
        };
    }

    // The grid can load before its header template exists, so the box is looked for again each frame until it does,
    // for as long as the grid stays loaded. A layout pass happens many times a frame while the rows build.
    private async Task WireSelectAllHeaderWhenReadyAsync(DataGrid grid, Func<CheckBox, Action> wire)
    {
        _headerUnwires.TryAdd(grid, static () => { });
        for (int frame = 0; frame < MaxHeaderFrames; frame++)
        {
            if (frame > 0)
                await RenderFrames.NextAsync();
            if (!grid.IsLoaded)
                return;
            if (FindSelectAllCheckBox(grid) is not { } checkBox)
                continue;

            if (_headerUnwires.Remove(grid, out var unwire))
                unwire();
            _headerUnwires[grid] = wire(checkBox);
            return;
        }
    }

    private const int MaxHeaderFrames = 600;

    // Arrow keys focus the row, not its check box, so Space ticks the selected rows the way a click on the box does.
    private void AppsDataGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space || e.OriginalSource is CheckBox || sender is not DataGrid grid)
            return;

        var rows = grid.SelectedItems.OfType<AppItemViewModel>().Where(r => r.IsSelectable).ToList();
        if (rows.Count == 0)
            return;

        SelectAllRows.Toggle(rows);
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
            // The box is in the column header, never in a row.
            if (child is DataGridRow)
                continue;
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

    // handledEventsToo: a row's check box or the grid may already have marked the right-tap handled. The content is
    // built each time its apps finish loading, so the handler goes on each new one; removing first keeps it single.
    private void ExternalAppsContent_Loaded(object sender, RoutedEventArgs e)
    {
        var content = (UIElement)sender;
        var handler = new RightTappedEventHandler(ExternalApp_RightTapped);
        content.RemoveHandler(UIElement.RightTappedEvent, handler);
        content.AddHandler(UIElement.RightTappedEvent, handler, true);
    }

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

    // Refresh, View and Install options give up their labels first; Install and Remove keep theirs longest.
    private void FitActionBar()
    {
        if (ActionBar.Parent is not FrameworkElement row || row.ActualWidth <= 0)
            return;

        UIElement?[] secondary = [RefreshLabel, ViewMenuLabel, InstallOptionsLabel];
        UIElement?[] primary = [InstallLabel, RemoveLabel, InstallToggleLabel, RemoveToggleLabel];
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

    // Every switch of tab or view builds the list again, so each tab and view keeps its own place for the session.
    // Track stays on the list's scroller for the list's whole life, so it is wired once per list.
    private void AppList_Loaded(object sender, RoutedEventArgs e)
    {
        var list = (ListViewBase)sender;
        list.Loaded -= AppList_Loaded;
        if (list.FindDescendant<ScrollViewer>() is not { } scroller)
            return;

        ScrollMemory.Track(list.Name, list, scroller);
        _listRestore = ScrollMemory.RestoreAsync(list.Name, list);
        _listRestore.FireAndForget(_logService);
    }

    // ItemsWrapGrid has no spacing, so a cell is the card plus one gap and the container pads by half of it. The width
    // left over becomes equal side padding, so the header, the Select-all bar and the cards share one centred column.
    private void CardView_SizeChanged(object sender, SizeChangedEventArgs e) => FitCards((GridView)sender);

    // A section card is as wide as the columns its rows fill, so the cards, the search box and the Select-all bar centre
    // as one column that gains a column whenever one more fits.
    private void SectionList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var list = (ListView)sender;
        double width = list.ActualWidth;
        if (width <= 0)
            return;

        var body = (Thickness)Resources["AppSectionBodyPadding"];
        var border = (Thickness)Resources["AppSectionBorderThickness"];
        // The rows' panel needs an exact fit, and layout rounding at 150% shaves a pixel or two off the card, which
        // dropped a column. A pixel of slack each side is invisible.
        const double RoundingSlack = 2;
        double chrome = body.Left + body.Right + border.Left + border.Right + RoundingSlack;
        double itemWidth = (double)Resources["CompactItemWidth"];
        double spacing = (double)Resources["CompactColumnSpacing"];
        var (cols, _) = UniformWrapPanel.Fit(width - chrome, itemWidth, 0, spacing, int.MaxValue);
        CentreColumns(list, width, cols * (itemWidth + spacing) - spacing + chrome);
        // Only the shown tab's list exists, so the other tab re-splits when its list is built and sized.
        if (list.Name == nameof(WindowsAppsCompactView))
        {
            if (cols == _windowsLineColumns)
                return;
            _windowsLineColumns = cols;
            SplitWindowsSections();
        }
        else if (cols != _externalLineColumns)
        {
            _externalLineColumns = cols;
            SplitExternalSections();
        }
    }

    // A column count change gives every list new lines, so it scrolls back to the top, as a re-sort does.
    private void SplitWindowsSections()
    {
        if (_windowsLineColumns > 0)
        {
            WindowsSectionLines = Resplit(WindowsSectionLines, ViewModel.WindowsAppsViewModel.Sections, _windowsLineColumns);
            NativeHeaps.ReleaseDroppedMemoryLater(_dispatcherService, _logService);
        }
    }

    private void SplitExternalSections()
    {
        if (_externalLineColumns > 0)
        {
            ExternalSectionLines = Resplit(ExternalSectionLines, ViewModel.ExternalAppsViewModel.Categories, _externalLineColumns);
            NativeHeaps.ReleaseDroppedMemoryLater(_dispatcherService, _logService);
        }
    }

    // The same lines keep the list it already has, so nothing resets.
    private IReadOnlyList<AppSectionLine> Resplit(IReadOnlyList<AppSectionLine>? current, IReadOnlyList<AppCategory> sections, int columns)
    {
        var lines = AppSectionLine.Split(sections, columns, SectionChrome(), current);
        return current is not null && lines.SequenceEqual(current, ReferenceEqualityComparer.Instance) ? current : lines;
    }

    private AppSectionChrome SectionChrome() => new(
        (CornerRadius)Application.Current.Resources["OverlayCornerRadius"],
        (Thickness)Resources["AppSectionBorderThickness"],
        (Thickness)Resources["AppSectionDividerThickness"],
        (Thickness)Resources["AppSectionBodyPadding"],
        (double)Resources["CompactRowSpacing"],
        (double)Resources["AppSectionSpacing"]);

    // A view's first SizeChanged comes before its GridView has built the panel, so the panel fits itself once it loads.
    private void CardPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (ItemsControl.GetItemsOwner((DependencyObject)sender) is GridView grid)
            FitCards(grid);
    }

    private void FitCards(GridView grid)
    {
        double width = grid.ActualWidth;
        if (grid.ItemsPanelRoot is not ItemsWrapGrid panel || width <= 0)
            return;

        double spacing = CellGap("CardCellPadding");
        var (cols, cellWidth) = UniformWrapPanel.Fit(
            width - spacing, (double)Resources["CardItemWidth"], (double)Resources["CardMinItemWidth"], spacing, int.MaxValue);
        panel.ItemWidth = cellWidth + spacing;
        // The card's MinHeight lets larger text grow it, and a wrap grid row does not grow with it.
        panel.ItemHeight = (double)Resources["CardCellHeight"] * _textScale + spacing;
        CentreColumns(grid, width, cols * panel.ItemWidth);
    }

    private double CellGap(string paddingKey) => ((Thickness)Resources[paddingKey]).Left + ((Thickness)Resources[paddingKey]).Right;

    // Layout rounding snaps the padding to whole screen pixels. Rounded up, it leaves the panel a fraction short of the
    // columns at 125% or 150% scaling and the panel drops one, so the padding is snapped down here first.
    private static void CentreColumns(ListViewBase list, double width, double columnsWidth)
    {
        double scale = list.XamlRoot?.RasterizationScale ?? 1.0;
        var side = System.Math.Max(0, System.Math.Floor((width - columnsWidth) / 2 * scale) / scale);
        list.Padding = new Thickness(side, list.Padding.Top, side, list.Padding.Bottom);
    }

    // A click lands on the card's toggle or the row's check box, which keep the focus off themselves; Space and Enter
    // on the focused cell arrive here.
    private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AppItemViewModel { IsSelectable: true } app)
            app.IsSelected = !app.IsSelected;
    }

    // Narrator reads the container that holds the focus, not the card inside it.
    private void AppGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is AppItemViewModel app)
            AutomationProperties.SetName(args.ItemContainer, app.Name);
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

        var collectionView = dataGrid.Name == nameof(WindowsAppsDataGrid)
            ? ViewModel.WindowsAppsViewModel.ItemsView
            : ViewModel.ExternalAppsViewModel.ItemsView;

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
        _columnSorts[dataGrid.Name] = (sortProperty, newDirection);
    }

}
