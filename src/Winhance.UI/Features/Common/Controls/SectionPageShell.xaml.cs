using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Every pixel a section page (Optimize, Customize) draws around its settings: the header, the
/// search box, the breadcrumb, the Quick Actions and View menus, the templated overview cards, and
/// the detail frame.
///
/// <para><b>Why this exists.</b> The two pages' XAML was 316 lines each and identical on 311 of
/// them — they differed only in <c>x:Class</c>, the <c>xmlns:local</c> alias, one comment, and the
/// page icon. Deduplicating the code-behind into <see cref="SectionPage"/> left that untouched,
/// because a shared base class can hold behaviour but not markup. This control is where the markup
/// goes, so a third section page is a root tag plus an icon rather than another 316-line copy.</para>
///
/// <para><b>Why a UserControl and not a ResourceDictionary.</b> Two of the templates below bind
/// <c>Click</c> to code-behind, and a <c>DataTemplate</c> in a shared ResourceDictionary has no
/// <c>x:Class</c> to bind to. A UserControl has one, so the templates come along — which is what
/// makes this an extraction of everything rather than of the static chrome only.</para>
///
/// <para><b>The seam with <see cref="SectionPage"/>.</b> This control owns the elements; the page
/// base owns the behaviour. <see cref="Chrome"/> hands over the named elements the base manipulates
/// (labels, visibilities, toggle states) — the "template parts" pattern, made explicit and
/// type-safe instead of going through <c>GetTemplateChild</c> and a cast. Handlers on non-templated
/// elements are wired by the base against those parts; the three below cannot be, because they fire
/// from inside a <c>DataTemplate</c> whose instances do not exist yet at wiring time. Those are
/// re-raised with their original sender and args, so the base's handlers bind to them unchanged.</para>
/// </summary>
public sealed partial class SectionPageShell : UserControl
{
    /// <summary>
    /// The page's icon, shown at 64px in the header and at 16px on the breadcrumb root.
    ///
    /// Strongly typed rather than a string on purpose: a mistyped icon name resolves through
    /// <c>Enum.TryParse</c> and renders nothing at all — no crash, no build error, no test failure.
    /// As an enum-typed property the XAML parser rejects an unknown name instead.
    /// </summary>
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

    /// <summary>
    /// The page's ViewModel, as much of it as the chrome binds to.
    ///
    /// A DependencyProperty because <c>x:Bind ... Mode=OneWay</c> below needs a change
    /// notification to re-evaluate against, and it is assigned after <c>InitializeComponent</c>
    /// (the page resolves its ViewModel from the container in its constructor). Set by
    /// <see cref="SectionPage.InitializeSectionPage"/> rather than bound from each page's XAML, so
    /// there is no binding-order question about whether the ViewModel exists yet.
    /// </summary>
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

    /// <summary>An overview card was clicked. Carries the card as sender; its Tag is the section key.</summary>
    public event RoutedEventHandler? SectionCardClicked;

    /// <summary>A breadcrumb flyout entry was clicked. Carries the button as sender; its Tag is the section key.</summary>
    public event RoutedEventHandler? SectionFlyoutItemClicked;

    /// <summary>A link inside a feature's outcome banner was clicked.</summary>
    public event EventHandler<FeatureOutcomeNavigationEventArgs>? OutcomeBannerNavigationRequested;

    public SectionPageShell()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// The elements <see cref="SectionPage"/> reads and writes directly — text, visibility, checked
    /// state — bundled once so the base does not reach into this control's generated fields.
    ///
    /// Built on demand rather than cached: it is asked for once, during page construction.
    /// </summary>
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
    // the section key off sender.Tag exactly as it did when the template lived in the page.

    private void SectionCard_Click(object sender, RoutedEventArgs e) =>
        SectionCardClicked?.Invoke(sender, e);

    private void SectionFlyoutItem_Click(object sender, RoutedEventArgs e) =>
        SectionFlyoutItemClicked?.Invoke(sender, e);

    private void OnOutcomeBannerNavigationRequested(object? sender, FeatureOutcomeNavigationEventArgs e) =>
        OutcomeBannerNavigationRequested?.Invoke(sender, e);
}
