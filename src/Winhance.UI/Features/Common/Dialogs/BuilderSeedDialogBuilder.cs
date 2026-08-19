using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Dialogs;

internal class BuilderSeedDialogBuilder
{
    private const string SeedGroupName = "BuilderSeed";

    private readonly ILocalizationService _localization;

    private RadioButton _recommendedRadio = null!;
    private RadioButton _windowsDefaultsRadio = null!;

    public BuilderSeedDialogBuilder(ILocalizationService localization)
    {
        _localization = localization;
    }

    // The caller is responsible for ConfigureDialog and ShowAsync.
    public ContentDialog Build()
    {
        var dialog = new ContentDialog
        {
            Title = _localization.GetString("Dialog_BuilderSeed_Title"),
            PrimaryButtonText = _localization.GetString("Dialog_BuilderSeed_Continue"),
            CloseButtonText = _localization.GetString("Button_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        _recommendedRadio = SeedRadio(_localization.GetString("Dialog_BuilderSeed_Recommended"));
        _windowsDefaultsRadio = SeedRadio(_localization.GetString("Dialog_BuilderSeed_WindowsDefaults"));

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Dialog_BuilderSeed_Description"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(SeedRadio(_localization.GetString("Dialog_BuilderSeed_CurrentMachine"), isChecked: true));
        panel.Children.Add(_recommendedRadio);
        panel.Children.Add(_windowsDefaultsRadio);

        dialog.Content = panel;
        return dialog;
    }

    // Must be called after ShowAsync returns.
    public BuilderSeed? ExtractResult(ContentDialogResult result)
    {
        if (result != ContentDialogResult.Primary)
            return null;
        if (_recommendedRadio.IsChecked == true)
            return BuilderSeed.Recommended;
        if (_windowsDefaultsRadio.IsChecked == true)
            return BuilderSeed.WindowsDefaults;
        return BuilderSeed.CurrentMachine;
    }

    private static RadioButton SeedRadio(string label, bool isChecked = false) => new()
    {
        Content = label,
        GroupName = SeedGroupName,
        IsChecked = isChecked
    };
}
