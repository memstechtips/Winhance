using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingsListView : UserControl
{
    public static readonly DependencyProperty GroupedSettingsSourceProperty =
        DependencyProperty.Register(
            nameof(GroupedSettingsSource),
            typeof(ICollectionView),
            typeof(SettingsListView),
            new PropertyMetadata(null, OnGroupedSettingsSourceChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty HasNoSearchResultsProperty =
        DependencyProperty.Register(
            nameof(HasNoSearchResults),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false));

    public ICollectionView? GroupedSettingsSource
    {
        get => (ICollectionView?)GetValue(GroupedSettingsSourceProperty);
        set => SetValue(GroupedSettingsSourceProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool IsNotLoading => !IsLoading;

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
            control.Bindings.Update();
        }
    }
}
