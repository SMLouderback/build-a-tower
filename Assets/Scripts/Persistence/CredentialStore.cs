using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BuildATower
{
    public interface ICredentialStore
    {
        void SaveRefresh(string accountId, string refreshToken);
        bool TryLoadRefresh(string accountId, out string refreshToken);
        void DeleteRefresh(string accountId);
    }

    public sealed class CredentialStore : ICredentialStore
    {
        const string TargetPrefix = "BuildATower/CloudSave/";

        public void SaveRefresh(string accountId, string refreshToken)
        {
            ValidateAccountId(accountId);
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var tokenBytes = Encoding.Unicode.GetBytes(refreshToken);
            var tokenBlob = Marshal.AllocHGlobal(tokenBytes.Length);
            try
            {
                Marshal.Copy(tokenBytes, 0, tokenBlob, tokenBytes.Length);
                var credential = new NativeCredential
                {
                    Type = NativeCredentialType.Generic,
                    TargetName = TargetName(accountId),
                    CredentialBlobSize = (uint)tokenBytes.Length,
                    CredentialBlob = tokenBlob,
                    Persist = NativeCredentialPersist.LocalMachine,
                    UserName = accountId
                };

                if (!CredWrite(ref credential, 0))
                    throw CredentialManagerFailure("save");
            }
            finally
            {
                Marshal.FreeHGlobal(tokenBlob);
            }
#else
            throw new PlatformNotSupportedException("Build-A-Tower cloud credentials require Windows Credential Manager.");
#endif
        }

        public bool TryLoadRefresh(string accountId, out string refreshToken)
        {
            ValidateAccountId(accountId);
            refreshToken = null;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!CredRead(TargetName(accountId), NativeCredentialType.Generic, 0, out var credentialPointer))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                    return false;

                throw CredentialManagerFailure("load");
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                    return false;

                var tokenBytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, tokenBytes, 0, tokenBytes.Length);
                refreshToken = Encoding.Unicode.GetString(tokenBytes);
                return !string.IsNullOrEmpty(refreshToken);
            }
            finally
            {
                CredFree(credentialPointer);
            }
#else
            throw new PlatformNotSupportedException("Build-A-Tower cloud credentials require Windows Credential Manager.");
#endif
        }

        public void DeleteRefresh(string accountId)
        {
            ValidateAccountId(accountId);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (CredDelete(TargetName(accountId), NativeCredentialType.Generic, 0))
                return;

            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
                throw CredentialManagerFailure("delete");
#else
            throw new PlatformNotSupportedException("Build-A-Tower cloud credentials require Windows Credential Manager.");
#endif
        }

        static string TargetName(string accountId)
        {
            return TargetPrefix + accountId;
        }

        static void ValidateAccountId(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException("Account id is required.", nameof(accountId));
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        const int ErrorNotFound = 1168;

        static InvalidOperationException CredentialManagerFailure(string operation)
        {
            return new InvalidOperationException(
                "Windows Credential Manager failed to " + operation + " Build-A-Tower cloud credentials. Error "
                + Marshal.GetLastWin32Error() + ".");
        }

        enum NativeCredentialType : uint
        {
            Generic = 1
        }

        enum NativeCredentialPersist : uint
        {
            LocalMachine = 2
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NativeCredential
        {
            public uint Flags;
            public NativeCredentialType Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public NativeCredentialPersist Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CredRead(
            string target,
            NativeCredentialType type,
            uint reservedFlag,
            out IntPtr credentialPointer);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CredDelete(string target, NativeCredentialType type, uint flags);

        [DllImport("Advapi32.dll", SetLastError = true)]
        static extern void CredFree(IntPtr buffer);
#endif
    }
}
