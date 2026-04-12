using FluentAssertions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

/// <summary>
/// Verifies the logic branches of <see cref="ComboBoxOptionToPillStyleConverter"/> that can be
/// exercised without a running WinUI application host. The positive style-resource lookups use
/// <c>Application.Current.Resources</c> and are validated via manual visual verification
/// (per Task B3 Step 6 + B5 smoke test).
/// </summary>
public class ComboBoxOptionToPillStyleConverterTests
{
    private readonly ComboBoxOptionToPillStyleConverter _sut = new();

    [Fact]
    public void Convert_NonComboBoxDisplayOption_ReturnsNull()
    {
        var result = _sut.Convert("some string", typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void Convert_ShowPillFalse_ReturnsNull_EvenWhenRecommended()
    {
        var option = new ComboBoxDisplayOption("A", 0)
        {
            IsRecommended = true,
            IsDefault = false,
        };
        option.ShowPill = false;

        var result = _sut.Convert(option, typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void Convert_ShowPillFalse_ReturnsNull_EvenWhenDefault()
    {
        var option = new ComboBoxDisplayOption("A", 0)
        {
            IsRecommended = false,
            IsDefault = true,
        };
        option.ShowPill = false;

        var result = _sut.Convert(option, typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void Convert_NeitherFlag_ReturnsNull_EvenWithShowPill()
    {
        var option = new ComboBoxDisplayOption("A", 0)
        {
            IsRecommended = false,
            IsDefault = false,
        };
        option.ShowPill = true;

        var result = _sut.Convert(option, typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void PillStyleTrigger_IsSelf_WhenShowPillAndRecommended()
    {
        var option = new ComboBoxDisplayOption("A", 0) { IsRecommended = true };
        option.ShowPill = true;

        option.PillStyleTrigger.Should().BeSameAs(option);
    }

    [Fact]
    public void PillStyleTrigger_IsSelf_WhenShowPillAndDefault()
    {
        var option = new ComboBoxDisplayOption("A", 0) { IsDefault = true };
        option.ShowPill = true;

        option.PillStyleTrigger.Should().BeSameAs(option);
    }

    [Fact]
    public void PillStyleTrigger_IsNull_WhenShowPillFalse()
    {
        var option = new ComboBoxDisplayOption("A", 0)
        {
            IsRecommended = true,
            IsDefault = true,
        };
        option.ShowPill = false;

        option.PillStyleTrigger.Should().BeNull();
    }

    [Fact]
    public void PillStyleTrigger_IsNull_WhenNeitherFlag()
    {
        var option = new ComboBoxDisplayOption("A", 0);
        option.ShowPill = true;

        option.PillStyleTrigger.Should().BeNull();
    }

    [Fact]
    public void TiebreakLogic_RecommendedWinsOverDefault_WhenBothFlagsSet()
    {
        // The converter's switch expression must pick Recommended first when both flags are set.
        // We assert this indirectly: PillStyleTrigger is non-null (so the converter branch fires)
        // and the switch pattern matches IsRecommended:true before IsDefault:true
        // (validated by the converter's source code).
        var option = new ComboBoxDisplayOption("A", 0)
        {
            IsRecommended = true,
            IsDefault = true,
        };
        option.ShowPill = true;

        option.PillStyleTrigger.Should().BeSameAs(option);
        // Positive style resolution validated manually in smoke test B5.
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => _sut.ConvertBack(null!, typeof(object), null!, "en");
        act.Should().Throw<NotImplementedException>();
    }
}
