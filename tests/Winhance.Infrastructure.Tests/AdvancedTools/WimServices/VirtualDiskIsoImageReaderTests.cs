using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class VirtualDiskIsoImageReaderTests
{
    [Fact]
    public void Attach_Always_RequestsReadOnlyAndNoDriveLetter()
    {
        var native = new Mock<IVirtualDiskNative>();
        native.Setup(n => n.Open(It.IsAny<string>())).Returns(new FakeVirtualDiskHandle());
        var reader = new VirtualDiskIsoImageReader(native.Object, Mock.Of<ILogService>());

        reader.Attach(@"C:\source.iso");

        native.Verify(n => n.Attach(
            It.IsAny<IVirtualDiskHandle>(),
            AttachFlags.ReadOnly | AttachFlags.NoDriveLetter));
    }

    [Fact]
    public void Attach_Always_NeverRequestsPermanentLifetime()
    {
        var native = new Mock<IVirtualDiskNative>();
        native.Setup(n => n.Open(It.IsAny<string>())).Returns(new FakeVirtualDiskHandle());
        var reader = new VirtualDiskIsoImageReader(native.Object, Mock.Of<ILogService>());

        reader.Attach(@"C:\source.iso");

        native.Verify(n => n.Attach(
            It.IsAny<IVirtualDiskHandle>(),
            It.Is<AttachFlags>(f => !f.HasFlag(AttachFlags.PermanentLifetime))));
    }

    [Fact]
    public void Dispose_CallerThrewMidCopy_DetachesAnyway()
    {
        var native = new Mock<IVirtualDiskNative>();
        var handle = new FakeVirtualDiskHandle();
        native.Setup(n => n.Open(It.IsAny<string>())).Returns(handle);
        var reader = new VirtualDiskIsoImageReader(native.Object, Mock.Of<ILogService>());

        try
        {
            using var attachment = reader.Attach(@"C:\source.iso");
            throw new InvalidOperationException("caller blew up mid-copy");
        }
        catch (InvalidOperationException)
        {
        }

        handle.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Attach_AttachFails_ClosesTheHandle()
    {
        var native = new Mock<IVirtualDiskNative>();
        var handle = new FakeVirtualDiskHandle();
        native.Setup(n => n.Open(It.IsAny<string>())).Returns(handle);
        native.Setup(n => n.Attach(It.IsAny<IVirtualDiskHandle>(), It.IsAny<AttachFlags>()))
              .Throws(new InvalidOperationException("attach refused"));
        var reader = new VirtualDiskIsoImageReader(native.Object, Mock.Of<ILogService>());

        Action act = () => reader.Attach(@"C:\source.iso");

        act.Should().Throw<InvalidOperationException>();
        handle.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Attach_Succeeds_ExposesTheResolvedVolumeRoot()
    {
        var native = new Mock<IVirtualDiskNative>();
        native.Setup(n => n.Open(It.IsAny<string>())).Returns(new FakeVirtualDiskHandle());
        native.Setup(n => n.GetVolumeRootPath(It.IsAny<IVirtualDiskHandle>()))
              .Returns(@"\\?\Volume{11111111-2222-3333-4444-555555555555}\");
        var reader = new VirtualDiskIsoImageReader(native.Object, Mock.Of<ILogService>());

        using var attachment = reader.Attach(@"C:\source.iso");

        attachment.RootPath.Should().Be(@"\\?\Volume{11111111-2222-3333-4444-555555555555}\");
    }

    private sealed class FakeVirtualDiskHandle : IVirtualDiskHandle
    {
        public bool IsClosed { get; private set; }

        public void Dispose() => IsClosed = true;
    }
}
