using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Cryptography;
using Windows.Win32.Security.WinTrust;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class AuthenticodeVerifier(ILogService log) : IAuthenticodeVerifier
{
    // Microsoft Root Certificate Authority 2010. Every Microsoft update package chains to it.
    internal const string MicrosoftRoot2010Thumbprint = "3B1EFD3A66EA28B16697394703A72CA340A05BD5";

    // The leaf is not pinned: Microsoft rotates the signing certificate, and two packages of one update carry different ones.
    internal const string MicrosoftLeafName = "Microsoft Corporation";

    internal sealed record SignatureFacts(string LeafName, string RootThumbprint);

    public bool IsMicrosoftSigned(string path)
    {
        SignatureFacts? facts;
        try
        {
            facts = Inspect(path);
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not read the signature of {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }

        if (facts is null)
        {
            log.LogWarning($"{Path.GetFileName(path)} has no valid Authenticode signature");
            return false;
        }

        if (!string.Equals(facts.RootThumbprint, MicrosoftRoot2010Thumbprint, StringComparison.OrdinalIgnoreCase))
        {
            log.LogWarning($"{Path.GetFileName(path)} is signed, but its chain ends at root {facts.RootThumbprint}, not Microsoft Root Certificate Authority 2010");
            return false;
        }

        if (!string.Equals(facts.LeafName, MicrosoftLeafName, StringComparison.Ordinal))
        {
            log.LogWarning($"{Path.GetFileName(path)} is signed by '{facts.LeafName}', not '{MicrosoftLeafName}'");
            return false;
        }

        return true;
    }

    // Null unless WinVerifyTrust accepts the file. Revocation is not checked: DISM verifies the signature again at
    // install, and the media is often built on a PC with no route to the revocation servers.
    internal static SignatureFacts? Inspect(string path)
    {
        if (!IsTrusted(path))
        {
            return null;
        }

        var cms = new SignedCms();
        cms.Decode(ReadEmbeddedSignature(path));
        try
        {
            var leaf = cms.SignerInfos.Count == 0 ? null : cms.SignerInfos[0].Certificate;
            if (leaf is null)
            {
                return null;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.ExtraStore.AddRange(cms.Certificates);

            // WinVerifyTrust has already judged the signature at its timestamp; Build only lays the chain out to read the
            // root, and a signing certificate that has since expired must still lay out.
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
            chain.Build(leaf);

            try
            {
                var elements = chain.ChainElements;
                return elements.Count == 0
                    ? null
                    : new SignatureFacts(leaf.GetNameInfo(X509NameType.SimpleName, false), elements[elements.Count - 1].Certificate.Thumbprint);
            }
            finally
            {
                foreach (var element in chain.ChainElements)
                {
                    element.Certificate.Dispose();
                }
            }
        }
        finally
        {
            foreach (var certificate in cms.Certificates)
            {
                certificate.Dispose();
            }
        }
    }

    // X509Certificate.CreateFromSignedFile is obsolete and X509CertificateLoader does not read Authenticode, so the
    // PKCS#7 blob comes out through CryptQueryObject and SignedCms parses it.
    private static unsafe byte[] ReadEmbeddedSignature(string path)
    {
        void* message = null;
        fixed (char* pathPtr = path)
        {
            if (!PInvoke.CryptQueryObject(
                CERT_QUERY_OBJECT_TYPE.CERT_QUERY_OBJECT_FILE,
                pathPtr,
                CERT_QUERY_CONTENT_TYPE_FLAGS.CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                CERT_QUERY_FORMAT_TYPE_FLAGS.CERT_QUERY_FORMAT_FLAG_BINARY,
                0,
                null,
                null,
                null,
                null,
                &message,
                null))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        try
        {
            uint size = 0;
            if (!PInvoke.CryptMsgGetParam(message, PInvoke.CMSG_ENCODED_MESSAGE, 0, null, &size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var blob = new byte[size];
            fixed (byte* blobPtr = blob)
            {
                if (!PInvoke.CryptMsgGetParam(message, PInvoke.CMSG_ENCODED_MESSAGE, 0, blobPtr, &size))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            return blob;
        }
        finally
        {
            _ = PInvoke.CryptMsgClose(message);
        }
    }

    private static unsafe bool IsTrusted(string path)
    {
        var action = PInvoke.WINTRUST_ACTION_GENERIC_VERIFY_V2;
        fixed (char* pathPtr = path)
        {
            var file = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)sizeof(WINTRUST_FILE_INFO),
                pcwszFilePath = new PCWSTR(pathPtr),
            };
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)sizeof(WINTRUST_DATA),
                dwUIChoice = WINTRUST_DATA_UICHOICE.WTD_UI_NONE,
                fdwRevocationChecks = WINTRUST_DATA_REVOCATION_CHECKS.WTD_REVOKE_NONE,
                dwUnionChoice = WINTRUST_DATA_UNION_CHOICE.WTD_CHOICE_FILE,
                dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_VERIFY,
            };
            data.Anonymous.pFile = &file;
            var noWindow = new HWND(new IntPtr(-1));

            try
            {
                return PInvoke.WinVerifyTrust(noWindow, &action, &data) == 0;
            }
            finally
            {
                data.dwStateAction = WINTRUST_DATA_STATE_ACTION.WTD_STATEACTION_CLOSE;
                _ = PInvoke.WinVerifyTrust(noWindow, &action, &data);
            }
        }
    }
}
