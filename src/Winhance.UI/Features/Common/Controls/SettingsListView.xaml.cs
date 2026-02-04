using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Reusable UserControl for displaying grouped settings in a ListView.
/// Encapsulates the common ListView structure used across all settings pages.
/// </summary>
public sealed partial class SettingsListView : UserControl
{
    /// <summary>
    /// Dependency property for the grouped settings source.
    /// </summary>
    public static readonly DependencyProperty GroupedSettingsSourceProperty =
        DependencyProperty.Register(
            nameof(GroupedSettingsSource),
            typeof(ICollectionView),
            typeof(SettingsListView),
            new PropertyMetadata(null, OnGroupedSettingsSourceChanged));

    /// <summary>
    /// Dependency property for the loading state.
    /// </summary>
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false, OnIsLoadingChanged));

    /// <summary>
    /// Dependency property for the no search results state.
    /// </summary>
    public static readonly DependencyProperty HasNoSearchResultsProperty =
        DependencyProperty.Register(
            nameof(HasNoSearchResults),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the grouped settings source for the ListView.
    /// </summary>
    public ICollectionView? GroupedSettingsSource
    {
        get => (ICollectionView?)GetValue(GroupedSettingsSourceProperty);
        set => SetValue(GroupedSettingsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the control is in a loading state.
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// Gets whether the control is not loading (inverse of IsLoading).
    /// </summary>
    public bool IsNotLoading => !IsLoading;

    /// <summary>
    /// Gets or sets whether to show the no search results message.
    /// </summary>
    public bool HasNoSearchResults
    {
        get => (bool)GetValue(HasNoSearchResultsProperty);
        set => SetValue(HasNoSearchResultsProperty, value);
    }

    public SettingsListView()
    {
        this.InitializeComponent();
    }

    private static void OnGroupedSettingsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
        {
            control.SettingsListViewControl.ItemsSource = e.NewValue as ICollectionView;
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
        {
            // Notify that IsNotLoading has also changed
            control.Bindings.Update();
        }
    }
}
