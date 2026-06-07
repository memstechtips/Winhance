using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Microsoft.Win32;
using System.Reflection;

namespace Winhance.Infrastructure.Tests.Services;

public class RegistryLockingTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUser = new();
    private readonly WindowsRegistryService _sut;

    public RegistryLockingTests()
    {
        _mockInteractiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        _sut = new WindowsRegistryService(_mockLog.Object, _mockInteractiveUser.Object);
    }

    [Theory]
    [InlineData(LockCondition.Always, true, 0, true)]
    [InlineData(LockCondition.Always, false, 0, true)]
    [InlineData(LockCondition.OnDisabled, true, 0, false)]
    [InlineData(LockCondition.OnDisabled, false, 0, true)]
    [InlineData(LockCondition.WhenValueIs4, true, 4, true)]
    [InlineData(LockCondition.WhenValueIs4, true, 2, false)]
    [InlineData(LockCondition.None, true, 4, false)]
    public void ShouldLock_CorrectlyEvaluatesConditions(LockCondition condition, bool isEnabled, object writtenValue, bool expected)
    {
        var setting = new RegistrySetting
        {
            KeyPath = @"HKLM\Software\Test",
            ValueName = "TestValue",
            RecommendedValue = 1,
            DefaultValue = 0,
            ValueType = RegistryValueKind.DWord,
            LockCondition = condition
        };

        var method = typeof(WindowsRegistryService).GetMethod("ShouldLock", BindingFlags.NonPublic | BindingFlags.Instance);
        var result = (bool)method.Invoke(_sut, new[] { setting, isEnabled, writtenValue });

        result.Should().Be(expected);
    }
}
