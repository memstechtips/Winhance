using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class FeatureBadgeAggregatorTests
{
    [Fact]
    public void Aggregate_WithEmptySettings_ReturnsZeroCounts()
    {
        var mockFeature = new Mock<ISettingsFeatureViewModel>();
        mockFeature.Setup(f => f.Settings)
            .Returns(new ObservableCollection<SettingItemViewModel>());

        var result = FeatureBadgeAggregator.Aggregate(mockFeature.Object);

        result.TotalWithBadgeData.Should().Be(0);
        result.RecommendedCount.Should().Be(0);
        result.DefaultCount.Should().Be(0);
        result.CustomCount.Should().Be(0);
        result.NewCount.Should().Be(0);
    }

    [Fact]
    public void Aggregate_WithNullSettings_ReturnsZeroCounts()
    {
        var mockFeature = new Mock<ISettingsFeatureViewModel>();
        mockFeature.Setup(f => f.Settings)
            .Returns((ObservableCollection<SettingItemViewModel>)null!);

        var result = FeatureBadgeAggregator.Aggregate(mockFeature.Object);

        result.TotalWithBadgeData.Should().Be(0);
        result.RecommendedCount.Should().Be(0);
        result.DefaultCount.Should().Be(0);
        result.CustomCount.Should().Be(0);
        result.NewCount.Should().Be(0);
    }
}
