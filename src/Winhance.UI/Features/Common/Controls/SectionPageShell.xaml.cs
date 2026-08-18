using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

// The markup both section pages share. A UserControl rather than a ResourceDictionary because two of the
// templates bind Click to code-behind, and a DataTemplate in a shared ResourceDictionary has no x:Class to bind
// to. Seam with SectionPage: this control owns the elements, the page base owns the behaviour; Chrome hands over
// the named parts (the template-parts pattern, type-safe instead of GetTemplateChild + cast).
public sealed partial class SectionPageShell : UserControl
{
    // Strongly typed on purpose: a mistyped icon name goes through Enum.TryParse and renders nothing - no crash, no
    // build error - whereas the XAML parser rejects an unknown enum name.
    public static readonly DependencyProperty PageIconProperty = DependencyProperty.Register(
        nameof(PageIcon),
        typeof(FluentIcons.Common.Icon),
        typeof(SectionPageShell),
        new PropertyMetadata(FluentIcons.Common.Icon.Settings));

    public FluentIcons.Common.Icon PageIcon
    {
        get => (FluentIcons.Common.Icon)GetValue(PageIconProperty);
        set => SetValue(PageIconProperty, value);
    }

    // A DependencyProperty because x:Bind Mode=OneWay needs change notification and it is assigned after
    // InitializeComponent; set by SectionPage.InitializeSectionPage so there is no binding-order question.
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ISectionPageViewModel),
        typeof(SectionPageShell),
        new PropertyMetadata(null));

    public ISectionPageViewModel? ViewModel
    {
        get => (ISectionPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Sender is the card; its Tag is the section key.
    public event RoutedEventHandler? SectionCardClicked;

    // Sender is the button; its Tag is the section key.
    public event RoutedEventHandler? SectionFlyoutItemClicked;

    public event EventHandler<FeatureOutcomeNavigationEventArgs>? OutcomeBannerNavigationRequested;

    public SectionPageShell()
    {
        this.InitializeComponent();
    }

    // Bundled once so the base does not reach into this control's generated fields; built on demand because it is
    // asked for once, during page construction.
    public SectionPageChrome Chrome => new(
        OverviewScroller: OverviewScrollView,
        OverviewContent: OverviewContent,
        ContentFrame: InnerContentFrame,
        SearchBox: SearchBox,
        BreadcrumbRoot: BreadcrumbRoot,
        BreadcrumbSeparator: BreadcrumbSeparator,
        BreadcrumbSection: BreadcrumbSection,
        BreadcrumbSectionText: BreadcrumbSectionText,
        BreadcrumbFlyout: BreadcrumbFlyout,
        QuickActionsLabel: QuickActionsLabel,
        QuickActionsButton: QuickActionsButton,
        ApplyRecommendedItem: ApplyRecommendedItem,
        ApplyRecommendedIcon: ApplyRecommendedIcon,
        ResetDefaultsItem: ResetDefaultsItem,
        ViewMenuLabel: ViewMenuLabel,
        ViewMenuButton: ViewMenuButton,
        TechnicalDetailsToggle: TechnicalDetailsToggleItem,
        InfoBadgesToggle: InfoBadgesToggleItem,
        NewBadgesToggle: NewBadgesToggleItem,
        ShowOnlyChangesToggle: ShowOnlyChangesToggleItem,
        ShowOnlyChangesSeparator: ShowOnlyChangesSeparator);

    // The three template-scoped handlers. Their instances are created per item as the ItemsControl
    // realizes them, so the base cannot subscribe to them element-by-element the way it does for the
    // chrome; the control forwards instead. Sender and args pass through untouched - the base reads
    // the section key off sender.Tag.

    private void SectionCard_Click(object sender, RoutedEventArgs e) =>
        SectionCardClicked?.Invoke(sender, e);

    private void SectionFlyoutItem_Click(object sender, RoutedEventArgs e) =>
        SectionFlyoutItemClicked?.Invoke(sender, e);

    private void OnOutcomeBannerNavigationRequested(object? sender, FeatureOutcomeNavigationEventArgs e) =>
        OutcomeBannerNavigationRequested?.Invoke(sender, e);
}
