//------------------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">colinw</owner>
// Security review: nzeng 01-27-06
//------------------------------------------------------------------------------

#region Using directives

using System;
using System.Text;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

#endregion

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
    internal sealed class SafeToken : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeToken()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            CredentialNativeMethods.CloseHandle(this.handle);
            return true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Shared source file")]
        internal IntPtr UnsafeHandle
        {
            get { return handle; }
        }

        internal static SafeToken LogonUser(string username, string password, int logonType, int logonProvider)
        {
            string domain;
            string user;
            int index = username.IndexOf('\\');

            if (0 <= index)
            {
                domain = username.Substring(0, index);
                user = username.Substring(index + 1);
            }
            else
            {
                //
                // No slash '\' in user account string. Assume local account or
                // a internet style account user@dns_domain_name
                //
                domain = null;
                user = username;
            }

            SafeToken token;
            if (!CredentialNativeMethods.LogonUser(user, domain, password, logonType, logonProvider, out token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return token;
        }
    }

    /// <summary>
    /// The NativeMethods class implements wrappers for native methods
    /// </summary>
    internal static class CredentialNativeMethods
    {
        [DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszUsername,
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszDomain,
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out SafeToken phToken
        );

        [DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszUsername,
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszDomain,
            IntPtr lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out SafeToken phToken
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [Flags]
        public enum CREDUI_FLAGS
        {
            INCORRECT_PASSWORD = 0x1,
            DO_NOT_PERSIST = 0x2,
            REQUEST_ADMINISTRATOR = 0x4,
            EXCLUDE_CERTIFICATES = 0x8,
            REQUIRE_CERTIFICATE = 0x10,
            SHOW_SAVE_CHECK_BOX = 0x40,
            ALWAYS_SHOW_UI = 0x80,
            REQUIRE_SMARTCARD = 0x100,
            PASSWORD_ONLY_OK = 0x200,
            VALIDATE_USERNAME = 0x400,
            COMPLETE_USERNAME = 0x800,
            PERSIST = 0x1000,
            SERVER_CREDENTIAL = 0x4000,
            EXPECT_CONFIRMATION = 0x20000,
            GENERIC_CREDENTIALS = 0x40000,
            USERNAME_TARGET_CREDENTIALS = 0x80000,
            KEEP_USERNAME = 0x100000,
        }

#pragma warning disable 0649
        public struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszMessageText;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }
#pragma warning restore 0649

        public const int CREDUI_ERROR_CANCELLED = 1223;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport("credui", EntryPoint = "CredUIPromptForCredentialsW", CharSet = CharSet.Unicode)]
        internal static extern int CredUIPromptForCredentials(ref CREDUI_INFO creditUR,
            string targetName,
            IntPtr reserved1,
            int iError,
            StringBuilder userName,
            int maxUserName,
            StringBuilder password,
            int maxPassword,
            ref int iSave,
            CREDUI_FLAGS flags);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [DllImport("credui", EntryPoint = "CredUICmdLinePromptForCredentialsW", CharSet = CharSet.Unicode)]
        internal static extern int CredUICmdLinePromptForCredentials(string targetName,
            IntPtr reserved1,
            int iError,
            StringBuilder userName,
            int maxUserName,
            StringBuilder password,
            int maxPassword,
            ref int iSave,
            CREDUI_FLAGS flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct OSVERSIONINFOEX
        {
            internal uint dwOSVersionInfoSize;
            internal uint dwMajorVersion;
            internal uint dwMinorVersion;
            internal uint dwBuildNumber;
            internal uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szCSDVersion;
            internal ushort wServicePackMajor;
            internal ushort wServicePackMinor;
            internal ushort wSuiteMask;
            internal byte wProductType;
            internal byte wReserved;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api")]
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetVersionEx(ref OSVERSIONINFOEX versionInfo);

        internal const int VER_NT_DOMAIN_CONTROLLER = 2;

        #region Managing local accounts

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int NetUserAdd(
             [MarshalAs(UnmanagedType.LPWStr)] string servername,
             UInt32 level,
             ref USER_INFO_1 userInfo,
             out UInt32 parm_err);


        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal extern static int NetUserGetInfo(
            [MarshalAs(UnmanagedType.LPWStr)] string ServerName,
            [MarshalAs(UnmanagedType.LPWStr)] string UserName,
            int level,
            out IntPtr BufPtr);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal extern static int NetUserSetInfo(
            [MarshalAs(UnmanagedType.LPWStr)] string ServerName,
            [MarshalAs(UnmanagedType.LPWStr)] string UserName,
            int level,
            IntPtr BufPtr,
            out UInt32 parm_err);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall,
        SetLastError = true)]
        internal static extern uint NetUserChangePassword(
        [MarshalAs(UnmanagedType.LPWStr)] string domainname,
        [MarshalAs(UnmanagedType.LPWStr)] string username,
        IntPtr oldpassword,
        IntPtr newpassword
        );


        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal extern static int NetUserDel(
            [MarshalAs(UnmanagedType.LPWStr)]string servername,
            [MarshalAs(UnmanagedType.LPWStr)] string username
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct USER_INFO_1
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string sUsername;
            public IntPtr sPassword;
            public uint uiPasswordAge;
            public uint uiPriv;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string sHome_Dir;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string sComment;
            public uint uiFlags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string sScript_Path;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct USER_INFO_1003
        {
            public IntPtr sPassword;
        }

        [DllImport("netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal extern static int NetLocalGroupAddMembers(string ServerName, string LocalGroupName,
            uint Level, ref LOCALGROUP_MEMBERS_INFO_3 MemberInfo, uint TotalEntries);

#pragma warning disable 0649
        internal struct LOCALGROUP_MEMBERS_INFO_3
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Domain;
        }
#pragma warning restore 0649

        internal const uint UF_SCRIPT = 0x0001;

        internal const uint USER_PRIV_USER = 1;
        internal const uint USER_PRIV_ADMIN = 2;

        internal const int NERR_Success = 0;
        internal const int NERR_UserNotFound = 2221;
        internal const int NERR_UserExists = 2224;
        internal const int NERR_PasswordTooShort = 2245;

        internal const int ERROR_INVALID_PASSWORD = 86;
        internal const int ERROR_MEMBER_IN_ALIAS = 1378;

        #endregion

    }
}
