//------------------------------------------------------------------------------
// <copyright file="Credentials.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    #region Using directives

    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;

    #endregion

    /// <summary>
    /// The Credentials class implements some common functionality for 
    /// validating user credentials and creating logon tokens
    /// </summary>
    internal static class Credentials
    {
        #region PublicMethods

        public const int LOGON32_LOGON_INTERACTIVE = 2;
        public const int LOGON32_LOGON_NETWORK = 3;
        public const int LOGON32_LOGON_BATCH = 4;
        public const int LOGON32_LOGON_SERVICE = 5;
        public const int LOGON32_LOGON_UNLOCK = 7;
        public const int LOGON32_LOGON_NETWORK_CLEARTEXT = 8;
        public const int LOGON32_LOGON_NEW_CREDENTIALS = 9;
        public const int LOGON32_PROVIDER_DEFAULT = 0;

        public const int DEBUG_PROCESS = 0x00000001;
        public const int DEBUG_ONLY_THIS_PROCESS = 0x00000002;
        public const int CREATE_SUSPENDED = 0x00000004;
        public const int DETACHED_PROCESS = 0x00000008;
        public const int CREATE_NEW_CONSOLE = 0x00000010;
        public const int NORMAL_PRIORITY_CLASS = 0x00000020;
        public const int IDLE_PRIORITY_CLASS = 0x00000040;
        public const int HIGH_PRIORITY_CLASS = 0x00000080;
        public const int REALTIME_PRIORITY_CLASS = 0x00000100;
        public const int CREATE_NEW_PROCESS_GROUP = 0x00000200;
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const int CREATE_SEPARATE_WOW_VDM = 0x00000800;
        public const int CREATE_SHARED_WOW_VDM = 0x00001000;
        public const int CREATE_FORCEDOS = 0x00002000;
        public const int CREATE_DEFAULT_ERROR_MODE = 0x04000000;
        public const int CREATE_NO_WINDOW = 0x08000000;

        public const int CRED_MAX_USERNAME_LENGTH = (256 + 1 + 256);
        public const int CRED_MAX_PASSWORD_LENGTH = 256;

        internal static int ValidateCredentials(string username, string password, bool throwException)
        {
            try
            {
                using (SafeToken token = LogonUserAndGetToken(username, password))
                {
                    return 0;
                }
            }
            catch (Win32Exception exception)
            {
                if (throwException)
                {
                    throw new System.Security.Authentication.AuthenticationException(exception.Message, exception);
                }

                return exception.ErrorCode;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Someone may use this function in the future")]
        internal static SafeToken GetTokenFromCredentials(string username, string password)
        {
            try
            {
                return LogonUserAndGetToken(username, password);
            }
            catch (Win32Exception exception)
            {
                throw new System.Security.Authentication.AuthenticationException(exception.Message, exception);
            }
        }


        static SafeToken LogonUserAndGetToken(string username, string password)
        {
            return SafeToken.LogonUser(
                                username,
                                password,
                                LOGON32_LOGON_NETWORK,
                                LOGON32_PROVIDER_DEFAULT);
        }


        const CredentialNativeMethods.CREDUI_FLAGS DefaultCredUIFlags =
            CredentialNativeMethods.CREDUI_FLAGS.GENERIC_CREDENTIALS |
            CredentialNativeMethods.CREDUI_FLAGS.EXCLUDE_CERTIFICATES |
            CredentialNativeMethods.CREDUI_FLAGS.DO_NOT_PERSIST |
            CredentialNativeMethods.CREDUI_FLAGS.ALWAYS_SHOW_UI;


        /// <summary>
        /// Prompt for credentials without showing the "Save" checkbox
        /// </summary>
        /// <param name="target">Target to the credentials</param>
        /// <param name="username">receives user name</param>
        /// <param name="password">receives password associated</param>
        /// <param name="fConsole">Is this a console application</param>
        /// <param name="hwndParent">Parent window handle</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Someone may use this function in the future")]
        internal static void PromptForCredentials(string target, ref string username, ref SecureString password, bool fConsole, IntPtr hwndParent)
        {
            bool fSave = false;
            PromptForCredentials(target, ref username, ref password, ref fSave, fConsole, hwndParent, DefaultCredUIFlags, null);
        }

        /// <summary>
        /// Prompt for credentials with default options. "Save" checkbox is shown.
        /// </summary>
        /// <param name="target">Target to the credentials</param>
        /// <param name="username">receives user name</param>
        /// <param name="password">receives password associated</param>
        /// <param name="fSave">receives if credentials are saved.</param>
        /// <param name="fConsole">Is this a console application</param>
        /// <param name="hwndParent">Parent window handle</param>
        internal static void PromptForCredentials(string target, ref string username, ref SecureString password, ref bool fSave, bool fConsole, IntPtr hwndParent)
        {
            CredentialNativeMethods.CREDUI_FLAGS flags = DefaultCredUIFlags |
                                             CredentialNativeMethods.CREDUI_FLAGS.SHOW_SAVE_CHECK_BOX;
            PromptForCredentials(target, ref username, ref password, ref fSave, fConsole, hwndParent, flags, null);
        }

        /// <summary>
        /// Prompt for credentials
        /// </summary>
        /// <param name="target">Target to the credentials</param>
        /// <param name="username">receives user name</param>
        /// <param name="password">receives password associated</param>
        /// <param name="fSave">receives if credentials are saved.</param>
        /// <param name="fConsole">Is this a console application</param>
        /// <param name="hwndParent">Parent window handle</param>
        /// <param name="flags">options for the window.</param>
        /// <param name="message">Message to end user included in the window.</param>
        internal static void PromptForCredentials(string target, ref string username, ref SecureString password, ref bool fSave, bool fConsole, IntPtr hwndParent, CredentialNativeMethods.CREDUI_FLAGS flags, string message)
        {
            CredentialNativeMethods.CREDUI_INFO info = new CredentialNativeMethods.CREDUI_INFO();
            int result;

            int saveCredentials;
            StringBuilder user = new StringBuilder(username, CRED_MAX_USERNAME_LENGTH);
            StringBuilder pwd = new StringBuilder(CRED_MAX_PASSWORD_LENGTH);
            saveCredentials = 0;
            info.cbSize = Marshal.SizeOf(info);
            info.hwndParent = hwndParent;

            if (!string.IsNullOrEmpty(message))
            {
                // Use default message text, when message is not specified.
                info.pszMessageText = message;
            }

            if (fConsole)
            {
                result = CredentialNativeMethods.CredUICmdLinePromptForCredentials(target,
                                                           IntPtr.Zero,
                                                           0,
                                                           user,
                                                           CRED_MAX_USERNAME_LENGTH,
                                                           pwd,
                                                           CRED_MAX_PASSWORD_LENGTH,
                                                           ref saveCredentials,
                                                           flags);
            }
            else
            {
                if (hwndParent == new IntPtr(-1))
                {
                    throw new System.Security.Authentication.InvalidCredentialException();
                }
                result = CredentialNativeMethods.CredUIPromptForCredentials(ref info,
                                                    target,
                                                    IntPtr.Zero,
                                                    0,
                                                    user,
                                                    CRED_MAX_USERNAME_LENGTH,
                                                    pwd,
                                                    CRED_MAX_PASSWORD_LENGTH,
                                                    ref saveCredentials,
                                                    flags);
            }
            if (result != 0)
            {
                throw new System.ComponentModel.Win32Exception(result);
            }

            fSave = (saveCredentials != 0);
            username = user.ToString();
            password = new SecureString();
            for (int i = 0; i < pwd.Length; i++)
            {
                password.AppendChar(pwd[i]);
                pwd[i] = ' ';                   // overwrite the cleartext password
            }
            password.MakeReadOnly();
        }

        //
        // helper function to convert a SecureString password back to a cleartext string
        //
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal static string UnsecureString(SecureString secureString)
        {
            if (secureString == null)
            {
                return null;
            }
            string unsecureString;

            IntPtr ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            try
            {
                unsecureString = Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
            return unsecureString;
        }
        #endregion


        #region Operate with local accounts

        internal static bool ExistsLocalAccount(string username)
        {
            IntPtr infoPtr = IntPtr.Zero;

            try
            {
                int errCode = CredentialNativeMethods.NetUserGetInfo(null, username, 1, out infoPtr);

                if (errCode == CredentialNativeMethods.NERR_Success)
                {
                    return true;
                }
                else if (errCode == CredentialNativeMethods.NERR_UserNotFound)
                {
                    return false;
                }

                throw new Exception("Error when checking whether the local account " + username + " exists. Error code: " + errCode);
            }
            finally
            {
                if (infoPtr != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(infoPtr);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// In this function, we assume the caller has already checked that the user exists.
        /// Check it using ExistsLocalAccount method.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="newPassword"></param>
        /// <param name="oldPassword"></param>
        internal static void SetLocalAccountPassword(string username, SecureString newPassword, SecureString oldPassword)
        {
            IntPtr newPlainPassword = IntPtr.Zero;
            IntPtr oldPlainPassword = IntPtr.Zero;

            try
            {
                if (newPassword == null)
                {
                    newPlainPassword = Marshal.StringToCoTaskMemUni(String.Empty);
                }
                else
                {
                    newPlainPassword = Marshal.SecureStringToCoTaskMemUnicode(newPassword);
                }


                if (oldPassword == null)
                {
                    oldPlainPassword = Marshal.StringToCoTaskMemUni(String.Empty);
                }
                else
                {
                    oldPlainPassword = Marshal.SecureStringToCoTaskMemUnicode(oldPassword);
                }

                // Note: the first parameter of NetUserChangePassword is domain name. If it
                // is NULL, the logon domain of the caller, instead of local computer, is used.
                // This is unlike NetUserAdd, NetUserGetInfo or NetUserSetInfo.
                uint errCode = CredentialNativeMethods.NetUserChangePassword(
                    Environment.MachineName, username, oldPlainPassword, newPlainPassword);

                if (errCode != CredentialNativeMethods.NERR_Success)
                {
                    switch (errCode)
                    {
                        case CredentialNativeMethods.NERR_UserNotFound:
                            throw new ArgumentException("The local user " + username + " is not found.");

                        case CredentialNativeMethods.NERR_PasswordTooShort:
                            // If password is long enough but still you receive error
                            // "NERR_PasswordTooShort"  in a DOMAIN environment, then it is probably
                            // an indication that the time span since the previous password change
                            // operation is shorter than the allowed policy. It is a policy in AD to 
                            // have a minimum password age, for example, of 1 day.
                            throw new ArgumentException("The password given is too short.");

                        case CredentialNativeMethods.ERROR_INVALID_PASSWORD:
                            throw new ArgumentException("The given old password is not valid.");

                        default:
                            throw new Exception("Error when trying to reset password for local account " + username + " exists. Error code: " + errCode);
                    }
                }
            }
            finally
            {
                if (newPlainPassword != IntPtr.Zero)
                {
                    Marshal.ZeroFreeCoTaskMemUnicode(newPlainPassword);
                }

                if (oldPlainPassword != IntPtr.Zero)
                {
                    Marshal.ZeroFreeCoTaskMemUnicode(oldPlainPassword);
                }
            }
        }

        /// <summary>
        /// Set password for a specified local account regardless of old password
        /// In this function, we assume the caller has already checked that the user exists.
        /// Check it using ExistsLocalAccount method.
        /// </summary>
        /// <param name="username">local account name</param>
        /// <param name="password">new password for the specified local account</param>
        internal static void SetLocalAccountPassword(string username, SecureString password)
        {
            CredentialNativeMethods.USER_INFO_1003 info = new CredentialNativeMethods.USER_INFO_1003();
            if (password == null)
            {
                info.sPassword = Marshal.StringToCoTaskMemUni(String.Empty);
            }
            else
            {
                info.sPassword = Marshal.SecureStringToCoTaskMemUnicode(password);
            }

            IntPtr infoPtr = IntPtr.Zero;
            try
            {
                infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(info));
                Marshal.StructureToPtr(info, infoPtr, false);
                uint errParam;
                int errCode = CredentialNativeMethods.NetUserSetInfo(null, username, /*information level of infoPtr =*/1003, infoPtr, out errParam);

                if (errCode != CredentialNativeMethods.NERR_Success)
                {
                    switch (errCode)
                    {
                        case CredentialNativeMethods.NERR_UserNotFound:
                            throw new ArgumentException("The local user " + username + " is not found.");
                        default:
                            throw new Exception("Error when setting password for local account " + username + ". Error code: " + errCode);
                    }
                }
            }
            finally
            {
                if (info.sPassword != IntPtr.Zero)
                {
                    Marshal.ZeroFreeCoTaskMemUnicode(info.sPassword);
                }

                if (infoPtr != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(infoPtr);
                    }
                    catch { }
                }
            }
        }


        /// <summary>
        /// In this function, we assume the caller has already checked that the user does not exist.
        /// Check it using ExistsLocalAccount method
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="isAdmin"></param>
        /// <param name="schedulerOnAzure"></param>
        internal static void AddLocalAccount(string username, string password, bool isAdmin, bool schedulerOnAzure)
        {
            // Call the SecureString version

            SecureString securePass = new SecureString();
            foreach (char c in password.ToCharArray())
            {
                securePass.AppendChar(c);
            }

            AddLocalAccount(username, securePass, isAdmin, schedulerOnAzure);
        }

        /// <summary>
        /// In this function, we assume the caller has already checked that the user does not exist.
        /// Check it using ExistsLocalAccount method
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="isAdmin"></param>
        /// <param name="schedulerOnAzure"></param>
        internal static void AddLocalAccount(string username, SecureString password, bool isAdmin, bool schedulerOnAzure)
        {
            CredentialNativeMethods.USER_INFO_1 info = new CredentialNativeMethods.USER_INFO_1();

            info.sUsername = username;
            if (password == null)
            {
                info.sPassword = Marshal.StringToCoTaskMemUni(String.Empty);
            }
            else
            {
                info.sPassword = Marshal.SecureStringToCoTaskMemUnicode(password);
            }
            info.uiPriv = CredentialNativeMethods.USER_PRIV_USER;
            info.sHome_Dir = null;
            info.sComment = null;
            info.sScript_Path = null;
            info.uiFlags = CredentialNativeMethods.UF_SCRIPT;

            // Check the string length to prevent memory overflow

            if (info.sUsername.Length > 128)
            {
                throw new ArgumentException("The user name is too long.");
            }

            if (password != null && password.Length > 512)
            {
                throw new ArgumentException("The password is too long.");
            }

            try
            {
                uint errParam = 0;
                int errCode = CredentialNativeMethods.NetUserAdd(null, 1, ref info, out errParam);

                if (errCode != CredentialNativeMethods.NERR_Success)
                {
                    switch (errCode)
                    {
                        case CredentialNativeMethods.NERR_UserExists:
                            throw new ArgumentException("The local user " + username + " already exists.");

                        case CredentialNativeMethods.NERR_PasswordTooShort:
                            throw new ArgumentException("The password given is too short.");

                        default:
                            throw new Exception("Error when trying to create local account " + username + " exists. Error code: " + errCode + ", sub-error: " + errParam);
                    }
                }
            }
            finally
            {
                if (info.sPassword != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.ZeroFreeCoTaskMemUnicode(info.sPassword);
                    }
                    catch { }
                }
            }

            AddUserGroups(username, isAdmin, schedulerOnAzure);
        }

        /// <summary>
        /// Add a local account to related user and administrator groups
        /// </summary>
        /// <param name="username">local accout name</param>
        /// <param name="isAdmin">a flag indicating if the specified local account is admin</param>
        /// <param name="schedulerOnAzure">a flag indicating if scheduler is on on Azure</param>
        internal static void AddUserGroups(string username, bool isAdmin, bool schedulerOnAzure)
        {
            if (isAdmin)
            {
                AddUserGroup(username, "Administrators");
                if (schedulerOnAzure)
                {
                    AddUserGroup(username, "HPCAdminMirror");
                }
            }

            AddUserGroup(username, "Users");
            if (schedulerOnAzure)
            {
                AddUserGroup(username, "HPCUsers");
            }
        }

        static void AddUserGroup(string username, string groupname)
        {
            CredentialNativeMethods.LOCALGROUP_MEMBERS_INFO_3 grpinfo;
            grpinfo.Domain = username;

            int errCode = 0;

            // add the user to the administrators group
            errCode = CredentialNativeMethods.NetLocalGroupAddMembers(null, groupname, 3, ref grpinfo, 1);

            if (errCode != 0 && errCode != CredentialNativeMethods.ERROR_MEMBER_IN_ALIAS)
            {
                throw new Exception("Error when trying to add local account " + username + " to group " + groupname + ". Error code:" + errCode);
            }
        }

        /// <summary>
        /// In this function, we assume the caller has already checked that the user exists.
        /// Check it using ExistsLocalAccount method.
        /// </summary>
        /// <param name="username"></param>
        internal static void DeleteLocalAccount(string username)
        {
            int errCode = CredentialNativeMethods.NetUserDel(null, username);

            if (errCode != CredentialNativeMethods.NERR_Success)
            {
                switch (errCode)
                {
                    case CredentialNativeMethods.NERR_UserNotFound:
                        throw new ArgumentException("The local user " + username + " is not found.");

                    default:
                        throw new Exception("Error when trying to delete local account " + username + " exists. Error code: " + errCode);
                }
            }
        }

        internal static string ToLocalAccount(string domainAccount)
        {
            if (string.IsNullOrWhiteSpace(domainAccount))
            {
                throw new ArgumentNullException("domainAccount");
            }

            string domainname = null;
            string username = null;

            if (domainAccount.Contains("\\"))
            {
                if (domainAccount.StartsWith("NT AUTHORITY\\", StringComparison.InvariantCultureIgnoreCase))
                {
                    return domainAccount;
                }

                try
                {
                    string[] strs = domainAccount.Split(new char[] { '\\' }, StringSplitOptions.None);
                    domainname = strs[0];
                    username = strs[1];
                }
                catch
                {
                    throw new ArgumentException();
                }
            }
            else if (domainAccount.Contains("@"))
            {
                try
                {
                    string[] strs = domainAccount.Split(new char[] { '@' }, StringSplitOptions.None);
                    username = strs[0];
                    domainname = strs[1];
                }
                catch
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                return domainAccount;
            }


            return username;
        }

        /// <summary>
        /// Maximum number of characters in a user name
        /// </summary>
        const int MaxAccountNameLength = 20;

        /// <summary>
        /// Maximum number of characers from the 
        /// domain user name to be used as the prefix
        /// of the generated user name
        /// </summary>
        const int MaxPrefixLength = 10;

        /// <summary>
        /// Maximum number of retries for hashing 
        /// </summary>
        const int MaxHashRetryCount = 50;

        /// <summary>
        /// Delimiters valid in a user name 
        /// </summary>
        private static readonly char[] delimiters = new char[] { '-', '_', ' ', '.', '#' };

        /// <summary>
        /// Invalid Base64 characters for a user name string
        /// </summary>
        private static readonly char[] invalidBase64UserNameChars =
            new char[] { '+', '/', '=' };

        /// <summary>
        /// Convert a domain account name into a unique local account name.
        /// </summary>
        /// <param name="domainAccount">domain account name</param>
        /// <returns>a unique local account name</returns>
        internal static string ToUniqueLocalAccount(string domainAccount)
        {
            if (string.IsNullOrWhiteSpace(domainAccount))
            {
                throw new ArgumentNullException("domainAccount");
            }

            string uniqueLocalAccount;

            if (domainAccount.StartsWith("NT AUTHORITY\\"))
            {
                uniqueLocalAccount = domainAccount;
            }
            else
            {
                // unique local account = Purged local name + suffix (hash code of domain account)
                string localname = Credentials.ToLocalAccount(domainAccount);

                // Remove all delimiters from the source
                string purged = RemoveChars(localname, delimiters);

                // We only take the first MaxPrefixLength characters from
                // the seed user name so as to guarantee at least 
                // MaxPrefixLength characters from the hash.
                if (purged.Length > MaxPrefixLength)
                {
                    purged = purged.Substring(0, MaxPrefixLength);
                }

                // The length is 10 or larger
                int suffixLength = MaxAccountNameLength - purged.Length;

                var builder = new StringBuilder();
                builder.Append(purged);

                using (SHA256 sha256 = SHA256.Create())
                {
                    string suffix = localname;

                    int retry = 0;
                    do
                    {
                        sha256.ComputeHash(Encoding.Default.GetBytes(suffix));
                        byte[] hash = sha256.Hash;
                        suffix = RemoveChars(Convert.ToBase64String(hash), invalidBase64UserNameChars);

                        retry++;
                        if (retry > MaxHashRetryCount)
                        {
                            throw new ApplicationException(
                                "Failed to create a hash suffix to create a user name after maximum retries.");
                        }
                    }
                    while (suffix.Length < suffixLength);

                    builder.Append(suffix.Substring(0, suffixLength));
                }

                uniqueLocalAccount = builder.ToString();

            }

            return uniqueLocalAccount;
        }

        /// <summary>
        /// Remove characters from the given list in a string
        /// </summary>
        /// <param name="source">The string to purge</param>
        /// <param name="toRemove">The list of characters to remove</param>
        /// <returns>The purged string</returns>
        private static string RemoveChars(string source, char[] toRemove)
        {
            Debug.Assert(
                string.IsNullOrEmpty(source) == false,
                "Input string should not be null or empty.");

            Debug.Assert(
                toRemove != null,
                "The array of characters to remove cannot be null.");

            var builder = new StringBuilder();

            for (int i = 0; i < source.Length; i++)
            {
                if (!toRemove.Any(d => d == source[i]))
                {
                    builder.Append(source[i]);
                }
            }

            return builder.ToString();
        }

        #endregion
    }
}
