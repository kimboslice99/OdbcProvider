/**
* This is a wrapper for our membershipprovider to allow com access
* Perhaps COM API should be restricted to an admin user somehow? TODO
* 
* Maybe expose some more useful methods?
* 
*/

using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Web.Security;

namespace OdbcProvider
{
    [Guid("8E71A13A-2226-4A2D-BBE3-6C619E4A1E1A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IOdbcMembershipProvider
    {
        void SetConnectionString(string connectionString);

        bool ValidateUser(string username, string password);
        bool ChangePassword(string username, string oldPassword, string newPassword);
        bool UnlockUser(string userName);
        bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer);
        bool DeleteUser(string username, bool deleteAllRelatedData);
        string GetUserNameByEmail(string email);
        string ResetPassword(string username, string answer);

        bool IsUserInRole(string username, string roleName);
        string[] GetRolesForUser(string username);
        string[] GetUsersInRole(string roleName);
        string[] GetAllRoles();
        bool RoleExists(string roleName);
        void CreateRole(string roleName);
        bool DeleteRole(string roleName, bool throwOnPopulatedRole);
        string[] FindUsersInRole(string roleName, string usernameToMatch);
        void RemoveUsersFromRoles(string[] usernames, string[] roleNames);
    }

    [Guid("DCF9ECC2-CE4C-46CA-8E8D-06556DEC8969")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class OdbcProviderWrapper : IOdbcMembershipProvider
    {
        private string _connectionString;
        private readonly OdbcMembershipProvider _provider;
        private readonly OdbcRoleProvider _odbcRoleProvider;
        private readonly Utils _Utils;
        private readonly object _initLock = new object();
        private bool _isProviderInitialized;
        private bool _isRoleProviderInitialized;

        public OdbcProviderWrapper()
        {
            _provider = new OdbcMembershipProvider();
            _odbcRoleProvider = new OdbcRoleProvider();
            _Utils = new Utils();
            _Utils.WriteDebug("OdbcMembershipProviderWrapper instance created.");
        }

        public void SetConnectionString(string connectionString)
        {
            lock (_initLock)
            {
                _connectionString = connectionString;
                _isProviderInitialized = false;
                _isRoleProviderInitialized = false;
            }
        }

        private void InitializeMembershipProvider()
        {
            if (_isProviderInitialized) return;

            lock (_initLock)
            {
                if (_isProviderInitialized) return;

                try
                {
                    var config = new NameValueCollection
                    {
                        { "connectionString", _connectionString },
                        { "description", "Odbc membership provider" }
                        // Add other necessary configurations
                    };

                    _provider.ComInit(config);
                    _Utils.WriteDebug("OdbcMembershipProvider initialized successfully.");
                    _isProviderInitialized = true;
                }
                catch (Exception ex)
                {
                    _Utils.WriteDebug($"Error initializing OdbcMembershipProvider: {ex.Message}");
                    throw; // Rethrow the exception to ensure the caller is aware of the failure
                }
            }
        }

        private void InitializeRoleProvider()
        {
            if (_isRoleProviderInitialized) return;

            lock (_initLock)
            {
                if (_isRoleProviderInitialized) return;

                try
                {
                    var config = new NameValueCollection
                    {
                        { "connectionString", _connectionString },
                        { "description", "Odbc membership provider" }
                        // Add other necessary configurations
                    };

                    _odbcRoleProvider.ComInit(config);
                    _Utils.WriteDebug("OdbcRoleProvider initialized successfully.");
                    _isRoleProviderInitialized = true;
                }
                catch (Exception ex)
                {
                    _Utils.WriteDebug($"Error initializing OdbcRoleProvider: {ex.Message}");
                    throw; // Rethrow the exception to ensure the caller is aware of the failure
                }
            }
        }

        public bool ValidateUser(string username, string password)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _Utils.ValidateUser(username, password, _connectionString), nameof(ValidateUser));
        }

        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _provider.ChangePassword(username, oldPassword, newPassword), nameof(ChangePassword));
        }

        public bool UnlockUser(string userName)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _provider.UnlockUser(userName), nameof(UnlockUser));
        }

        public bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _provider.ChangePasswordQuestionAndAnswer(username, password, newPasswordQuestion, newPasswordAnswer), nameof(ChangePasswordQuestionAndAnswer));
        }

        public bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _provider.DeleteUser(username, deleteAllRelatedData), nameof(DeleteUser));
        }

        public string GetUserNameByEmail(string email)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _provider.GetUserNameByEmail(email), nameof(GetUserNameByEmail));
        }

        public string ResetPassword(string username, string answer)
        {
            InitializeMembershipProvider();
            return ExecuteWithLogging(() => _provider.ResetPassword(username, answer), nameof(ResetPassword));
        }

        public bool IsUserInRole(string username, string roleName)
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() => _odbcRoleProvider.IsUserInRole(username, roleName), nameof(IsUserInRole));
        }

        public string[] GetRolesForUser(string username)
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() => _odbcRoleProvider.GetRolesForUser(username), nameof(GetRolesForUser));
        }

        public string[] GetUsersInRole(string roleName)
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() => _odbcRoleProvider.GetUsersInRole(roleName), nameof(GetUsersInRole));
        }

        public string[] GetAllRoles()
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() => _odbcRoleProvider.GetAllRoles(), nameof(GetAllRoles));
        }

        public bool RoleExists(string roleName)
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() => _odbcRoleProvider.RoleExists(roleName), nameof(RoleExists));
        }

        public void CreateRole(string roleName)
        {
            InitializeRoleProvider();
            ExecuteWithLogging(() => { _odbcRoleProvider.CreateRole(roleName); return true; }, nameof(CreateRole));
        }

        public bool DeleteRole(string roleName, bool throwOnPopulatedRole)
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() =>
            {
                _odbcRoleProvider.DeleteRole(roleName, throwOnPopulatedRole);
                return true;
            }, nameof(DeleteRole));
        }

        public void AddUsersToRoles(string[] usernames, string[] roleNames)
        {
            InitializeRoleProvider();
            ExecuteWithLogging(() =>
            {
                _odbcRoleProvider.AddUsersToRoles(usernames, roleNames);
            }, nameof(AddUsersToRoles));
        }

        public string[] FindUsersInRole(string roleName, string usernameToMatch)
        {
            InitializeRoleProvider();
            return ExecuteWithLogging(() =>
            {
                return _odbcRoleProvider.FindUsersInRole(roleName, usernameToMatch);
            }, nameof(FindUsersInRole));
        }

        public void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
        {
            InitializeRoleProvider();
            ExecuteWithLogging(() =>
            {
               _odbcRoleProvider.RemoveUsersFromRoles(usernames, roleNames);
            }, nameof(FindUsersInRole));
        }

        /// <summary>
        /// A helper function to reduce code duplication
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private T ExecuteWithLogging<T>(Func<T> func, string methodName)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error in {methodName}: {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// void helper
        /// </summary>
        /// <param name="action"></param>
        /// <param name="methodName"></param>
        private void ExecuteWithLogging(Action action, string methodName)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error in {methodName}: {ex.Message}");
                throw; // Rethrow the exception to ensure the caller is aware of the failure
            }
        }
    }
}