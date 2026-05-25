using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.AdvancedTools.ViewModels;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.AdvancedTools;

/// <summary>
/// WIM Utility page for creating custom Windows installation images.
/// </summary>
public sealed partial class WimUtilPage : Page
{
    private readonly ILocalizationService? _localizationService;
    private readonly List<WizardActionCard> _subscribedCards = new();

    public WimUtilViewModel ViewModel { get; }

    public WimUtilPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<WimUtilViewModel>();
        _localizationService = App.Services.GetService<ILocalizationService>();
        ActualThemeChanged += (_, _) => UpdateWinhanceXmlCardIcon();
        UpdateWinhanceXmlCardIcon();

        // PageUp/PageDown fast-scroll + Home/End jump (issue #581).
        PageScrollHelper.Attach(this, PageScrollView);
    }

    private void UpdateWinhanceXmlCardIcon()
    {
        var uri = ActualTheme == ElementTheme.Light
            ? "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png"
            : "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";
        WinhanceXmlCardIcon.Source = new BitmapImage(new Uri(uri));
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Set main window reference for file dialogs
        if (App.MainWindow != null)
        {
            ViewModel.SetMainWindow(App.MainWindow);
        }

        // Initialize the ViewModel
        await ViewModel.OnNavigatedToAsync();

        // Live-region announcements when wizard action cards transition state (issue #647).
        SubscribeToActionCards();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        UnsubscribeFromActionCards();
    }

    private void SubscribeToActionCards()
    {
        UnsubscribeFromActionCards();

        // Every card the user can see a spinner/checkmark/X on.
        WizardActionCard?[] cards =
        {
            ViewModel.SelectIsoCard, ViewModel.SelectDirectoryCard, ViewModel.ConvertImageCard,
            ViewModel.GenerateWinhanceXmlCard, ViewModel.DownloadXmlCard, ViewModel.SelectXmlCard,
            ViewModel.ExtractSystemDriversCard, ViewModel.SelectCustomDriversCard,
            ViewModel.DownloadOscdimgCard, ViewModel.SelectOutputCard,
        };

        foreach (var card in cards)
        {
            if (card == null) continue;
            card.PropertyChanged += OnCardPropertyChanged;
            _subscribedCards.Add(card);
        }
    }

    private void UnsubscribeFromActionCards()
    {
        foreach (var card in _subscribedCards)
        {
            card.PropertyChanged -= OnCardPropertyChanged;
        }
        _subscribedCards.Clear();
    }

    private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WizardActionCard card) return;

        // Only announce state transitions into a "noteworthy" state — the source-generated
        // PropertyChanged only fires on actual value changes, so this won't repeat.
        string? message = e.PropertyName switch
        {
            nameof(WizardActionCard.IsProcessing) when card.IsProcessing
                => $"{card.Title}: {_localizationService?.GetString("Accessibility_InProgress") ?? "in progress"}",
            nameof(WizardActionCard.IsComplete) when card.IsComplete
                => $"{card.Title}: {_localizationService?.GetString("Accessibility_Complete") ?? "complete"}",
            nameof(WizardActionCard.HasFailed) when card.HasFailed
                => $"{card.Title}: {_localizationService?.GetString("Accessibility_Failed") ?? "failed"}",
            _ => null,
        };
        if (message != null) Announce(message);
    }

    private void Announce(string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(this)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "WimUtil");
    }

    private void Windows10Download_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWindows10DownloadCommand.Execute(null);
    }

    private void Windows11Download_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWindows11DownloadCommand.Execute(null);
    }

    private void SchneegansLink_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSchneegansXmlGeneratorCommand.Execute(null);
    }
}
