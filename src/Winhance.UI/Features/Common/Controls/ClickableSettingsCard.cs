using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Winhance.UI.Features.Common.Controls;

// The toolkit's SettingsCardAutomationPeer offers the Invoke pattern for a clickable card but does not implement
// IInvokeProvider (8.2.251219), so Narrator, Voice Access and UI Automation get InvalidCastException when they
// press it. ButtonBase.Click can't be raised from outside the base class, so mouse, keyboard and automation all
// raise Invoked instead.
public partial class ClickableSettingsCard : SettingsCard
{
    private const string ActionIconMaxSizeKey = "SettingsCardActionIconMaxSize";
    private const string ActionIconMarginKey = "SettingsCardActionIconMargin";

    public event RoutedEventHandler? Invoked;

    public ClickableSettingsCard()
    {
        IsClickEnabled = true;
        Click += (sender, e) => Invoked?.Invoke(sender, e);
    }

    // The toolkit draws a ">" chevron on every clickable card. False hides it, for a card that picks an option or
    // expands in place rather than opening a page. Set it in XAML, before the template applies.
    public bool ShowsActionIcon
    {
        get => !Resources.ContainsKey(ActionIconMaxSizeKey);
        set
        {
            if (value)
            {
                Resources.Remove(ActionIconMaxSizeKey);
                Resources.Remove(ActionIconMarginKey);
            }
            else
            {
                Resources[ActionIconMaxSizeKey] = 0d;
                Resources[ActionIconMarginKey] = new Thickness(0);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => new ClickableSettingsCardAutomationPeer(this);

    internal void InvokeFromAutomation() => Invoked?.Invoke(this, new RoutedEventArgs());
}

internal partial class ClickableSettingsCardAutomationPeer : SettingsCardAutomationPeer, IInvokeProvider
{
    public ClickableSettingsCardAutomationPeer(ClickableSettingsCard owner) : base(owner) { }

    public void Invoke()
    {
        if (Owner is ClickableSettingsCard card)
            card.InvokeFromAutomation();
    }
}
