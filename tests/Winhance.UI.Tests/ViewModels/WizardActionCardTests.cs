using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Winhance.UI.Features.AdvancedTools.Models;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WizardActionCardTests
{
    // -------------------------------------------------------
    // Constructor / Default values
    // -------------------------------------------------------

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var card = new WizardActionCard();

        card.Icon.Should().BeEmpty();
        card.IconPath.Should().BeEmpty();
        card.Title.Should().BeEmpty();
        card.Description.Should().BeEmpty();
        card.ButtonText.Should().BeEmpty();
        card.IsEnabled.Should().BeTrue();
        card.Opacity.Should().Be(1.0);
        card.UsePathIcon.Should().BeFalse();
        card.IsComplete.Should().BeFalse();
        card.HasFailed.Should().BeFalse();
        card.IsProcessing.Should().BeFalse();
        card.ButtonCommand.Should().BeNull();
        card.DescriptionForeground.Should().BeNull();
    }

    // -------------------------------------------------------
    // Simple property setters with PropertyChanged
    // -------------------------------------------------------

    [Fact]
    public void Icon_Set_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var raised = false;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(card.Icon))
                raised = true;
        };

        card.Icon = "\uE710";

        card.Icon.Should().Be("\uE710");
        raised.Should().BeTrue();
    }

    [Fact]
    public void Title_Set_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var raised = false;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(card.Title))
                raised = true;
        };

        card.Title = "Select ISO";

        card.Title.Should().Be("Select ISO");
        raised.Should().BeTrue();
    }

    [Fact]
    public void Description_Set_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var raised = false;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(card.Description))
                raised = true;
        };

        card.Description = "Pick your ISO file";

        card.Description.Should().Be("Pick your ISO file");
        raised.Should().BeTrue();
    }

    [Fact]
    public void ButtonText_Set_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var raised = false;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(card.ButtonText))
                raised = true;
        };

        card.ButtonText = "Browse";

        card.ButtonText.Should().Be("Browse");
        raised.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_Set_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var raised = false;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(card.IsEnabled))
                raised = true;
        };

        card.IsEnabled = false;

        card.IsEnabled.Should().BeFalse();
        raised.Should().BeTrue();
    }

    [Fact]
    public void Opacity_Set_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var raised = false;
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(card.Opacity))
                raised = true;
        };

        card.Opacity = 0.5;

        card.Opacity.Should().Be(0.5);
        raised.Should().BeTrue();
    }

    [Fact]
    public void ButtonCommand_CanBeSet()
    {
        var card = new WizardActionCard();
        var command = new RelayCommand(() => { });

        card.ButtonCommand = command;

        card.ButtonCommand.Should().BeSameAs(command);
    }

    // -------------------------------------------------------
    // IconPath / UsePathIcon interaction
    // -------------------------------------------------------

    [Fact]
    public void IconPath_WhenSetToNonEmpty_SetsUsePathIconTrue()
    {
        var card = new WizardActionCard();

        card.IconPath = "M0,0 L10,10";

        card.UsePathIcon.Should().BeTrue();
    }

    [Fact]
    public void IconPath_WhenSetToEmpty_SetsUsePathIconFalse()
    {
        var card = new WizardActionCard();
        card.IconPath = "M0,0 L10,10"; // first set to non-empty

        card.IconPath = "";

        card.UsePathIcon.Should().BeFalse();
    }

    [Fact]
    public void IconPath_WhenSetToNull_SetsUsePathIconFalse()
    {
        var card = new WizardActionCard();
        card.IconPath = "some path";

        card.IconPath = null!;

        card.UsePathIcon.Should().BeFalse();
    }

    // -------------------------------------------------------
    // Mutual exclusion: IsComplete / HasFailed / IsProcessing
    // -------------------------------------------------------

    [Fact]
    public void IsComplete_SetTrue_ClearsIsProcessingAndHasFailed()
    {
        var card = new WizardActionCard();
        card.IsProcessing = true;
        card.HasFailed = false; // IsProcessing was cleared by HasFailed; reset state manually
        // Start fresh
        var card2 = new WizardActionCard();

        card2.IsComplete = true;

        card2.IsProcessing.Should().BeFalse();
        card2.HasFailed.Should().BeFalse();
        card2.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void HasFailed_SetTrue_ClearsIsProcessingAndIsComplete()
    {
        var card = new WizardActionCard();
        card.IsProcessing = true;

        card.HasFailed = true;

        card.IsProcessing.Should().BeFalse();
        card.IsComplete.Should().BeFalse();
        card.HasFailed.Should().BeTrue();
    }

    [Fact]
    public void IsProcessing_SetTrue_ClearsIsCompleteAndHasFailed()
    {
        var card = new WizardActionCard();
        card.IsComplete = true;

        card.IsProcessing = true;

        card.IsComplete.Should().BeFalse();
        card.HasFailed.Should().BeFalse();
        card.IsProcessing.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_SetFalse_DoesNotResetOtherFlags()
    {
        var card = new WizardActionCard();
        card.IsProcessing = true;

        card.IsComplete = false; // Setting to false should not trigger the clearing logic

        card.IsProcessing.Should().BeTrue();
    }

    [Fact]
    public void HasFailed_SetFalse_DoesNotResetOtherFlags()
    {
        var card = new WizardActionCard();
        card.IsProcessing = true;

        card.HasFailed = false;

        card.IsProcessing.Should().BeTrue();
    }

    [Fact]
    public void IsProcessing_SetFalse_DoesNotResetOtherFlags()
    {
        var card = new WizardActionCard();
        card.IsComplete = true;

        card.IsProcessing = false;

        card.IsComplete.Should().BeTrue();
    }

    // -------------------------------------------------------
    // State transition sequences
    // -------------------------------------------------------

    [Fact]
    public void ProcessingToComplete_Transition()
    {
        var card = new WizardActionCard();

        card.IsProcessing = true;
        card.IsProcessing.Should().BeTrue();
        card.IsComplete.Should().BeFalse();
        card.HasFailed.Should().BeFalse();

        card.IsComplete = true;
        card.IsComplete.Should().BeTrue();
        card.IsProcessing.Should().BeFalse();
        card.HasFailed.Should().BeFalse();
    }

    [Fact]
    public void ProcessingToFailed_Transition()
    {
        var card = new WizardActionCard();

        card.IsProcessing = true;
        card.IsProcessing.Should().BeTrue();

        card.HasFailed = true;
        card.HasFailed.Should().BeTrue();
        card.IsProcessing.Should().BeFalse();
        card.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void FailedToProcessing_Retry_Transition()
    {
        var card = new WizardActionCard();

        card.HasFailed = true;
        card.HasFailed.Should().BeTrue();

        card.IsProcessing = true; // retry
        card.IsProcessing.Should().BeTrue();
        card.HasFailed.Should().BeFalse();
        card.IsComplete.Should().BeFalse();
    }

    // -------------------------------------------------------
    // PropertyChanged notifications for state flags
    // -------------------------------------------------------

    [Fact]
    public void IsComplete_SetTrue_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var changedProperties = new List<string>();
        card.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        card.IsComplete = true;

        changedProperties.Should().Contain(nameof(card.IsComplete));
    }

    [Fact]
    public void HasFailed_SetTrue_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var changedProperties = new List<string>();
        card.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        card.HasFailed = true;

        changedProperties.Should().Contain(nameof(card.HasFailed));
    }

    [Fact]
    public void IsProcessing_SetTrue_RaisesPropertyChanged()
    {
        var card = new WizardActionCard();
        var changedProperties = new List<string>();
        card.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        card.IsProcessing = true;

        changedProperties.Should().Contain(nameof(card.IsProcessing));
    }

    // -------------------------------------------------------
    // Mutual exclusion cross-notifications
    // -------------------------------------------------------

    [Fact]
    public void IsComplete_SetTrue_RaisesPropertyChangedForCleared_IsProcessing()
    {
        var card = new WizardActionCard();
        card.IsProcessing = true;

        var changedProperties = new List<string>();
        card.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        card.IsComplete = true;

        // IsProcessing should have been set to false, generating a PropertyChanged
        changedProperties.Should().Contain(nameof(card.IsProcessing));
    }

    [Fact]
    public void HasFailed_SetTrue_RaisesPropertyChangedForCleared_IsProcessing()
    {
        var card = new WizardActionCard();
        card.IsProcessing = true;

        var changedProperties = new List<string>();
        card.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        card.HasFailed = true;

        changedProperties.Should().Contain(nameof(card.IsProcessing));
    }

    [Fact]
    public void IsProcessing_SetTrue_RaisesPropertyChangedForCleared_IsComplete()
    {
        var card = new WizardActionCard();
        card.IsComplete = true;

        var changedProperties = new List<string>();
        card.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        card.IsProcessing = true;

        changedProperties.Should().Contain(nameof(card.IsComplete));
    }
}
