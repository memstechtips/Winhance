using FluentAssertions;
using Winhance.UI.Features.WimUtil.Models;
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
        };
    }

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

        state.StatusText = "Ready";

        raised.Should().BeFalse();
    }

    [Fact]
    public void IsExpanded_SetTrue_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        state.IsExpanded = true;

        changedProperties.Should().Contain(nameof(state.IsExpanded));
    }

    [Fact]
    public void IsExpanded_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        var raised = false;
        state.PropertyChanged += (_, _) => raised = true;

        state.IsExpanded = false;

        raised.Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_SetTrue_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        state.IsAvailable = true;

        changedProperties.Should().Contain(nameof(state.IsAvailable));
        changedProperties.Should().Contain(nameof(state.IsLocked));
    }

    [Fact]
    public void IsAvailable_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        var raised = false;
        state.PropertyChanged += (_, _) => raised = true;

        state.IsAvailable = false;

        raised.Should().BeFalse();
    }

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

    [Fact]
    public void IsComplete_SetTrue_RaisesPropertyChanged()
    {
        var state = CreateState();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        state.IsComplete = true;

        changedProperties.Should().Contain(nameof(state.IsComplete));
    }

    [Fact]
    public void IsComplete_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var state = CreateState();
        var raised = false;
        state.PropertyChanged += (_, _) => raised = true;

        state.IsComplete = false;

        raised.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("\uE958", true)]
    public void HasGlyph_ForGlyph_ShowsGlyphInsteadOfFluentIcon(string? glyph, bool hasGlyph)
    {
        var state = new WizardStepState { Glyph = glyph };

        state.HasGlyph.Should().Be(hasGlyph);
        state.UsesFluentIcon.Should().Be(!hasGlyph);
    }

    [Fact]
    public void TransitionFromLockedToAvailable_UnlocksTheStep()
    {
        var state = CreateState();
        state.IsAvailable = false;

        state.IsLocked.Should().BeTrue();

        state.IsAvailable = true;

        state.IsLocked.Should().BeFalse();
    }
}
