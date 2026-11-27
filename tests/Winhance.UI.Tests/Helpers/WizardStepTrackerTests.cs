using FluentAssertions;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class WizardStepTrackerTests
{
    private static readonly double[] Tops = [0, 600, 1400, 2200];

    [Fact]
    public void AtTheTop_IsStep1()
    {
        WizardStepTracker.CurrentStep(Tops, 0, 800, 2000).Should().Be(1);
    }

    [Theory]
    [InlineData(300, 1)]
    [InlineData(460, 2)]
    [InlineData(1250, 3)]
    public void StepBecomesCurrentOnceItsTopPassesTheAnchorLine(double offset, int expected)
    {
        WizardStepTracker.CurrentStep(Tops, offset, 800, 2000).Should().Be(expected);
    }

    [Fact]
    public void AtTheBottom_IsTheLastStep_EvenWhenItsTopIsUnreachable()
    {
        WizardStepTracker.CurrentStep(Tops, 2000, 800, 2000).Should().Be(4);
    }

    [Fact]
    public void WhenEverythingFits_IsStep1()
    {
        WizardStepTracker.CurrentStep(Tops, 0, 3000, 0).Should().Be(1);
    }

    [Fact]
    public void AnchorIsCapped_SoATallViewportDoesNotRunAhead()
    {
        WizardStepTracker.CurrentStep(Tops, 450, 4000, 5000).Should().Be(2);
    }

    [Fact]
    public void NoSteps_IsStep1()
    {
        WizardStepTracker.CurrentStep([], 100, 800, 2000).Should().Be(1);
    }
}
