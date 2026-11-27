using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

// The stock InfoBarAutomationPeer hardcodes AutomationLiveSetting.Assertive in GetLiveSettingCore(), ignoring
// AutomationProperties.LiveSetting, so every visible InfoBar is read aloud when Narrator enters a page. This peer
// keeps the banner accessible on focus without interrupting on navigation.
public partial class QuietInfoBar : InfoBar
{
    protected override AutomationPeer OnCreateAutomationPeer()
        => new QuietInfoBarAutomationPeer(this);
}

internal partial class QuietInfoBarAutomationPeer : FrameworkElementAutomationPeer
{
    public QuietInfoBarAutomationPeer(QuietInfoBar owner) : base(owner) { }

    protected override AutomationLiveSetting GetLiveSettingCore()
        => AutomationLiveSetting.Off;

    protected override string GetClassNameCore() => "InfoBar";

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.StatusBar;

    protected override string GetNameCore()
    {
        if (Owner is InfoBar infoBar && !string.IsNullOrEmpty(infoBar.Message))
            return $"{infoBar.Severity}: {infoBar.Message}";
        return base.GetNameCore();
    }
}
