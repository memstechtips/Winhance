using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// CreateRestorePoint itself is never called: it writes a real restore point on whatever machine runs the suite.
public class NativeSystemRestorePointWriterTests
{
    private const int MaxLength = NativeSystemRestorePointWriter.MaxDescriptionLength;
    private const string Key = NativeSystemRestorePointWriter.SystemRestoreKeyPath;
    private const string Name = NativeSystemRestorePointWriter.FrequencyValueName;

    private readonly Dictionary<string, object> _values = [];
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly NativeSystemRestorePointWriter _sut;

    public NativeSystemRestorePointWriterTests()
    {
        _registry.Setup(r => r.GetValue(Key, Name)).Returns(() => _values.GetValueOrDefault(Name));
        _registry.Setup(r => r.SetValue(Key, Name, It.IsAny<object>(), RegistryValueKind.DWord))
            .Returns((string _, string _, object value, RegistryValueKind _) => { _values[Name] = value; return true; });
        _registry.Setup(r => r.DeleteValue(Key, Name)).Returns(() => _values.Remove(Name) || true);
        _sut = new NativeSystemRestorePointWriter(_registry.Object, Mock.Of<ILogService>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithFrequencyLimitLifted_ExistingValue_IsZeroForTheCallAndPutBackAfter(bool callSucceeds)
    {
        _values[Name] = 1440;
        object? seen = null;

        var result = _sut.WithFrequencyLimitLifted(lifted => { seen = _values[Name]; return lifted && callSucceeds; });

        result.Should().Be(callSucceeds);
        seen.Should().Be(0);
        _values[Name].Should().Be(1440);
    }

    [Fact]
    public void WithFrequencyLimitLifted_CallThrows_StillPutsTheValueBack()
    {
        _values[Name] = 60;

        var act = () => _sut.WithFrequencyLimitLifted<bool>(_ => throw new OperationCanceledException());

        act.Should().Throw<OperationCanceledException>();
        _values[Name].Should().Be(60);
    }

    [Fact]
    public void WithFrequencyLimitLifted_AbsentValue_IsZeroForTheCallAndAbsentAfter()
    {
        object? seen = null;

        _sut.WithFrequencyLimitLifted(lifted => { seen = _values[Name]; return lifted; }).Should().BeTrue();

        seen.Should().Be(0);
        _values.Should().NotContainKey(Name);
    }

    [Fact]
    public void WithFrequencyLimitLifted_ValueAlreadyZero_WritesNothing()
    {
        _values[Name] = 0;

        _sut.WithFrequencyLimitLifted(lifted => lifted).Should().BeTrue();

        _registry.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _registry.Verify(r => r.DeleteValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void WithFrequencyLimitLifted_ValueOfAnotherKind_IsLeftAloneAndTheCallIsNotLifted()
    {
        _values[Name] = "1440";

        _sut.WithFrequencyLimitLifted(lifted => lifted).Should().BeFalse();

        _values[Name].Should().Be("1440");
    }

    [Fact]
    public void WithFrequencyLimitLifted_LiftRefused_CallIsNotLiftedAndNothingIsPutBack()
    {
        _values[Name] = 1440;
        _registry.Setup(r => r.SetValue(Key, Name, It.IsAny<object>(), RegistryValueKind.DWord)).Returns(false);

        _sut.WithFrequencyLimitLifted(lifted => lifted).Should().BeFalse();

        _values[Name].Should().Be(1440);
        _registry.Verify(r => r.DeleteValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(MaxLength, MaxLength)]
    [InlineData(MaxLength + 40, MaxLength)]
    public void TruncateDescription_AnyLength_ReturnsThePrefixThatFitsTheBuffer(int length, int expectedLength)
    {
        // The last character differs from the rest, so keeping the wrong end would not still match.
        var description = new string('x', length - 1) + "Z";

        var truncated = NativeSystemRestorePointWriter.TruncateDescription(description);

        truncated.Should().Be(description[..expectedLength]);
    }
}
