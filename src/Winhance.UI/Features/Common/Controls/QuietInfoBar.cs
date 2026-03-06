using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// InfoBar subclass that does not auto-announce as a live region.
/// The stock InfoBarAutomationPeer hardcodes AutomationLiveSetting.Assertive
/// in GetLiveSettingCore(), ignoring any AutomationProperties.LiveSetting
/// set externally. This causes every visible InfoBar to be read aloud when
/// Narrator enters a page. QuietInfoBar replaces that peer so the banner
/// is still accessible on focus but doesn't interrupt on page navigation.
/// </summary>
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
