using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil;

public class AuthenticodeVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinhanceAuthenticode_" + Guid.NewGuid().ToString("N"));

    // CoreLib carries an embedded Authenticode signature (not a catalog one) and is in every .NET run, whatever the test host is.
    private static readonly string SignedFile = typeof(object).Assembly.Location;

    public AuthenticodeVerifierTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void MicrosoftRoot2010_IsInTheWindowsRootStore_UnderThePinnedThumbprint()
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        var found = store.Certificates.Find(X509FindType.FindByThumbprint, AuthenticodeVerifier.MicrosoftRoot2010Thumbprint, validOnly: false);

        found.Should().ContainSingle();
        found[0].Subject.Should().Contain("Microsoft Root Certificate Authority 2010");
    }

    [Fact]
    public void Inspect_EmbeddedSignedMicrosoftFile_ReadsTheSignerAndItsRoot()
    {
        var facts = AuthenticodeVerifier.Inspect(SignedFile);

        facts.Should().NotBeNull();
        facts!.LeafName.Should().NotBeNullOrEmpty();
        facts.RootThumbprint.Should().HaveLength(40);
    }

    // CoreLib is signed by '.NET', not 'Microsoft Corporation': a valid Microsoft signature from the wrong signer.
    [Fact]
    public void IsMicrosoftSigned_ValidSignatureFromAnotherSigner_IsFalse()
    {
        new AuthenticodeVerifier(Mock.Of<ILogService>()).IsMicrosoftSigned(SignedFile).Should().BeFalse();
    }

    [Fact]
    public void Inspect_CopyWithOneByteFlipped_IsNotTrusted()
    {
        var tampered = Path.Combine(_root, "tampered.dll");
        File.Copy(SignedFile, tampered);
        using (var stream = new FileStream(tampered, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Position = stream.Length / 2;
            var value = stream.ReadByte();
            stream.Position = stream.Length / 2;
            stream.WriteByte((byte)(value ^ 0xFF));
        }

        AuthenticodeVerifier.Inspect(tampered).Should().BeNull();
        new AuthenticodeVerifier(Mock.Of<ILogService>()).IsMicrosoftSigned(tampered).Should().BeFalse();
    }

    [Fact]
    public void IsMicrosoftSigned_UnsignedFile_IsFalse()
    {
        var plain = Path.Combine(_root, "plain.msu");
        File.WriteAllText(plain, "not a package");

        new AuthenticodeVerifier(Mock.Of<ILogService>()).IsMicrosoftSigned(plain).Should().BeFalse();
    }
}
