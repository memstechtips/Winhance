using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Dialogs;

internal class BuilderSeedDialogBuilder
{
    private const string SeedGroupName = "BuilderSeed";

    private readonly ILocalizationService _localization;
    private readonly ModeEntrySections _sections;

    private RadioButton _recommendedRadio = null!;
    private RadioButton _windowsDefaultsRadio = null!;
    private RadioButton _configFileRadio = null!;

    public BuilderSeedDialogBuilder(ILocalizationService localization, ModeEntryNotes notes)
    {
        _localization = localization;
        _sections = new ModeEntrySections(localization, notes);
    }

    // The caller is responsible for ConfigureDialog and ShowAsync.
    public ContentDialog Build()
    {
        var dialog = new ContentDialog
        {
            Title = _localization.GetString("Dialog_BuilderIntro_Title"),
            PrimaryButtonText = _localization.GetString("Dialog_BuilderIntro_Confirm"),
            CloseButtonText = _localization.GetString("Button_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        _recommendedRadio = SeedRadio(_localization.GetString("Dialog_BuilderSeed_Recommended"));
        _windowsDefaultsRadio = SeedRadio(_localization.GetString("Dialog_BuilderSeed_WindowsDefaults"));
        _configFileRadio = SeedRadio(_localization.GetString("Dialog_BuilderSeed_ConfigFile"));

        var panel = new StackPanel { Spacing = 12 };
        if (_sections.Build() is { } sections)
            panel.Children.Add(sections);
        panel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Dialog_BuilderSeed_Title"),
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Dialog_BuilderSeed_Description"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(SeedRadio(_localization.GetString("Dialog_BuilderSeed_CurrentMachine"), isChecked: true));
        panel.Children.Add(_recommendedRadio);
        panel.Children.Add(_windowsDefaultsRadio);
        panel.Children.Add(_configFileRadio);

        dialog.Content = panel;
        return dialog;
    }

    // Must be called after ShowAsync returns.
    public (BuilderSeed? Seed, bool HideExplanation) ExtractResult(ContentDialogResult result) =>
        result == ContentDialogResult.Primary ? (ChosenSeed(), _sections.HideExplanation) : (null, false);

    private BuilderSeed ChosenSeed()
    {
        if (_recommendedRadio.IsChecked == true)
            return BuilderSeed.Recommended;
        if (_windowsDefaultsRadio.IsChecked == true)
            return BuilderSeed.WindowsDefaults;
        if (_configFileRadio.IsChecked == true)
            return BuilderSeed.ConfigFile;
        return BuilderSeed.CurrentMachine;
    }

    private static RadioButton SeedRadio(string label, bool isChecked = false) => new()
    {
        Content = label,
        GroupName = SeedGroupName,
        IsChecked = isChecked
    };
}
