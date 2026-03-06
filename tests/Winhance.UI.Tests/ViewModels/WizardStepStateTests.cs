using FluentAssertions;
using Winhance.UI.Features.AdvancedTools.Models;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class WizardStepStateTests
{
    private WizardStepState CreateState()
    {
        return new WizardStepState
        {
            StepNumber = 1,
            Title = "Step One",
            Icon = "icon1",
        };
    }

    // -------------------------------------------------------
    // Default values
    // -------------------------------------------------------

    [Fact]
    public void DefaultState_IsExpanded_IsFalse()
    {
        var state = new WizardStepState();

        state.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void DefaultState_IsAvailable_IsFalse()
    {
        var state = new WizardStepState();

        state.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void DefaultState_IsComplete_IsFalse()
    {
        var state = new WizardStepState();

        state.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void DefaultState_StatusText_IsEmpty()
    {
        var state = new WizardStepState();

        state.StatusText.Should().BeEmpty();
    }

    // -------------------------------------------------------
    // Simple properties
    // -------------------------------------------------------

    [Fact]
    public void StepNumber_CanBeSet()
    {
        var state = CreateState();

        state.StepNumber.Should().Be(1);
    }

    [Fact]
    public void Title_CanBeSet()
    {
        var state = CreateState();

        state.Title.Should().Be("Step One");
    }

    [Fact]
    public void Icon_CanBeSet()
    {
        var state = CreateState();

        state.Icon.Should().Be("icon1");
    }

    // -------------------------------------------------------
    // StatusText with PropertyChanged
    // -------------------------------------------------------

    [Fact]
    public void StatusText_Set_RaisesPropertyChanged()
    {
        var state = CreateState();
        var raised = false;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(state.StatusText))
                raised = true;
        };

        state.StatusText = "Processing...";

        state.StatusText.Should().Be("Processing...");
        raised.Should().BeTrue();
    }

    [Fact]
    public void StatusText_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        state.StatusText = "Ready";

        var raised = false;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(state.StatusText))
                raised = true;
        };

        state.StatusText = "Ready"; // same value

        raised.Should().BeFalse();
    }

    // -------------------------------------------------------
    // IsExpanded with PropertyChanged and ChevronRotation
    // -------------------------------------------------------

    [Fact]
    public void IsExpanded_SetTrue_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        state.IsExpanded = true;

        changedProperties.Should().Contain(nameof(state.IsExpanded));
        changedProperties.Should().Contain(nameof(state.ChevronRotation));
    }

    [Fact]
    public void IsExpanded_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        // Default is false; set to false again
        var raised = false;
        state.PropertyChanged += (_, _) => raised = true;

        state.IsExpanded = false;

        raised.Should().BeFalse();
    }

    // -------------------------------------------------------
    // ChevronRotation (computed)
    // -------------------------------------------------------

    [Fact]
    public void ChevronRotation_WhenExpanded_Returns180()
    {
        var state = CreateState();

        state.IsExpanded = true;

        state.ChevronRotation.Should().Be(180);
    }

    [Fact]
    public void ChevronRotation_WhenCollapsed_Returns0()
    {
        var state = CreateState();

        state.IsExpanded = false;

        state.ChevronRotation.Should().Be(0);
    }

    // -------------------------------------------------------
    // IsAvailable with PropertyChanged, IsLocked, ShowChevron
    // -------------------------------------------------------

    [Fact]
    public void IsAvailable_SetTrue_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        state.IsAvailable = true;

        changedProperties.Should().Contain(nameof(state.IsAvailable));
        changedProperties.Should().Contain(nameof(state.IsLocked));
        changedProperties.Should().Contain(nameof(state.ShowChevron));
    }

    [Fact]
    public void IsAvailable_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        var raised = false;
        state.PropertyChanged += (_, _) => raised = true;

        state.IsAvailable = false; // default is false

        raised.Should().BeFalse();
    }

    // -------------------------------------------------------
    // IsLocked (computed: !IsAvailable)
    // -------------------------------------------------------

    [Fact]
    public void IsLocked_WhenNotAvailable_ReturnsTrue()
    {
        var state = CreateState();

        state.IsAvailable = false;

        state.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void IsLocked_WhenAvailable_ReturnsFalse()
    {
        var state = CreateState();

        state.IsAvailable = true;

        state.IsLocked.Should().BeFalse();
    }

    // -------------------------------------------------------
    // IsComplete with PropertyChanged and ShowChevron
    // -------------------------------------------------------

    [Fact]
    public void IsComplete_SetTrue_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        state.IsComplete = true;

        changedProperties.Should().Contain(nameof(state.IsComplete));
        changedProperties.Should().Contain(nameof(state.ShowChevron));
    }

    [Fact]
    public void IsComplete_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        var raised = false;
        state.PropertyChanged += (_, _) => raised = true;

        state.IsComplete = false; // default is false

        raised.Should().BeFalse();
    }

    // -------------------------------------------------------
    // ShowChevron (computed: !IsLocked && !IsComplete)
    // -------------------------------------------------------

    [Fact]
    public void ShowChevron_WhenAvailableAndNotComplete_ReturnsTrue()
    {
        var state = CreateState();
        state.IsAvailable = true;
        state.IsComplete = false;

        state.ShowChevron.Should().BeTrue();
    }

    [Fact]
    public void ShowChevron_WhenLocked_ReturnsFalse()
    {
        var state = CreateState();
        state.IsAvailable = false;
        state.IsComplete = false;

        state.ShowChevron.Should().BeFalse();
    }

    [Fact]
    public void ShowChevron_WhenComplete_ReturnsFalse()
    {
        var state = CreateState();
        state.IsAvailable = true;
        state.IsComplete = true;

        state.ShowChevron.Should().BeFalse();
    }

    [Fact]
    public void ShowChevron_WhenLockedAndComplete_ReturnsFalse()
    {
        var state = CreateState();
        state.IsAvailable = false;
        state.IsComplete = true;

        state.ShowChevron.Should().BeFalse();
    }

    // -------------------------------------------------------
    // Transition scenarios
    // -------------------------------------------------------

    [Fact]
    public void TransitionFromLockedToAvailable_UpdatesIsLockedAndShowChevron()
    {
        var state = CreateState();
        state.IsAvailable = false;

        state.IsLocked.Should().BeTrue();
        state.ShowChevron.Should().BeFalse();

        state.IsAvailable = true;

        state.IsLocked.Should().BeFalse();
        state.ShowChevron.Should().BeTrue();
    }

    [Fact]
    public void TransitionFromAvailableToComplete_HidesChevron()
    {
        var state = CreateState();
        state.IsAvailable = true;

        state.ShowChevron.Should().BeTrue();

        state.IsComplete = true;

        state.ShowChevron.Should().BeFalse();
    }

    [Fact]
    public void ExpandCollapseCycle_TogglesChevronRotation()
    {
        var state = CreateState();

        state.ChevronRotation.Should().Be(0);

        state.IsExpanded = true;
        state.ChevronRotation.Should().Be(180);

        state.IsExpanded = false;
        state.ChevronRotation.Should().Be(0);
    }
}
