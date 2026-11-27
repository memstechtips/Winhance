using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.UI.Features.Common.Dialogs;

internal class InstallOptionsDialogBuilder
{
    private readonly ILocalizationService _localization;

    private ToggleSwitch? _followDefaultsSwitch;
    private ComboBox _scopeBox = null!;
    private ComboBox _architectureBox = null!;
    private CheckBox _interactiveBox = null!;

    public InstallOptionsDialogBuilder(ILocalizationService localization)
    {
        _localization = localization;
    }

    // The caller is responsible for ConfigureDialog and ShowAsync. A null appName builds the defaults dialog.
    public ContentDialog Build(string? appName, InstallOptionsChoice current, InstallOptions defaults)
    {
        var dialog = new ContentDialog
        {
            Title = appName is null
                ? _localization.GetString("InstallOptions_Title")
                : _localization.GetString("InstallOptions_Title_App", appName),
            PrimaryButtonText = _localization.GetString("Button_OK"),
            CloseButtonText = _localization.GetString("Button_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        _scopeBox = OptionBox(_localization.GetString("InstallOptions_Scope_Label"),
            _localization.GetString("InstallOptions_Scope_Default"),
            _localization.GetString("InstallOptions_Scope_User"),
            _localization.GetString("InstallOptions_Scope_Machine"));
        _architectureBox = OptionBox(_localization.GetString("InstallOptions_Architecture_Label"),
            _localization.GetString("InstallOptions_Architecture_Default"),
            _localization.GetString("InstallOptions_Architecture_X64"),
            _localization.GetString("InstallOptions_Architecture_X86"),
            _localization.GetString("InstallOptions_Architecture_Arm64"));
        _interactiveBox = new CheckBox { Content = _localization.GetString("InstallOptions_Interactive") };
        Show(current.Options);
        SetEditable(!current.FollowsDefaults);

        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };

        if (appName is not null)
        {
            var followSwitch = new ToggleSwitch
            {
                Header = _localization.GetString("InstallOptions_FollowDefaults"),
                OnContent = _localization.GetString("Common_On"),
                OffContent = _localization.GetString("Common_Off"),
                IsOn = current.FollowsDefaults
            };
            // Switching back off restores what the user had before, not the defaults it was showing.
            var own = current.Options;
            followSwitch.Toggled += (_, _) =>
            {
                if (followSwitch.IsOn)
                {
                    own = Read();
                    Show(defaults);
                }
                else
                {
                    Show(own);
                }
                SetEditable(!followSwitch.IsOn);
            };
            _followDefaultsSwitch = followSwitch;
            panel.Children.Add(followSwitch);
        }

        panel.Children.Add(new TextBlock
        {
            Text = _localization.GetString(appName is null ? "InstallOptions_Description_Defaults" : "InstallOptions_Description_App"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(_scopeBox);
        panel.Children.Add(_architectureBox);
        panel.Children.Add(_interactiveBox);
        panel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("InstallOptions_Note"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.7
        });

        dialog.Content = panel;
        return dialog;
    }

    // Must be called after ShowAsync returns.
    public InstallOptionsChoice? ExtractResult(ContentDialogResult result)
    {
        if (result != ContentDialogResult.Primary)
            return null;
        return new InstallOptionsChoice(_followDefaultsSwitch?.IsOn == true, Read());
    }

    // Items are listed in enum order, so the selected index is the enum value.
    private InstallOptions Read() => new()
    {
        Scope = (InstallScope)_scopeBox.SelectedIndex,
        Architecture = (InstallArchitecture)_architectureBox.SelectedIndex,
        Interactive = _interactiveBox.IsChecked == true
    };

    private void Show(InstallOptions options)
    {
        _scopeBox.SelectedIndex = (int)options.Scope;
        _architectureBox.SelectedIndex = (int)options.Architecture;
        _interactiveBox.IsChecked = options.Interactive;
    }

    private void SetEditable(bool editable)
    {
        _scopeBox.IsEnabled = editable;
        _architectureBox.IsEnabled = editable;
        _interactiveBox.IsEnabled = editable;
    }

    private static ComboBox OptionBox(string header, params string[] items) => new()
    {
        Header = header,
        ItemsSource = items,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
