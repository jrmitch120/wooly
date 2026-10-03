using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Wooly.Core.Credentials;

/// <summary>
///     Windows' Credential Manager through its own API in <c>advapi32.dll</c>, which every Windows has: no Git, and
///     nothing installed alongside this client (ADR-0003).
/// </summary>
/// <remarks>
///     Generic credentials, persisted for this user on this machine (<c>CRED_PERSIST_LOCAL_MACHINE</c>): kept across
///     sign-ins, not carried to other machines by a roaming profile, and readable by nobody but this Windows user.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class CredentialManagerApi : ICredentialManager
{
    private const uint Generic = 1;
    private const uint LocalMachine = 2;
    private const int NotFound = 1168;

    /// <inheritdoc />
    public string? Read(string target)
    {
        if (!CredRead(target, Generic, 0, out var pointer))
        {
            var error = Marshal.GetLastPInvokeError();

            return error == NotFound ? null : throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);

            return credential.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    /// <inheritdoc />
    public void Write(string target, string userName, string secret)
    {
        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocHGlobal(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);

            var credential = new Credential
            {
                Type = Generic,
                TargetName = target,
                UserName = userName,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = LocalMachine,
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
        finally
        {
            // The token in the clear in this process's memory, for no longer than the write takes.
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
        }
    }

    /// <inheritdoc />
    public bool Delete(string target)
    {
        if (CredDelete(target, Generic, 0))
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();

        return error == NotFound ? false : throw new Win32Exception(error);
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    /// <summary>The API's <c>CREDENTIALW</c>, field for field.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}
