/**
 * This is a wrapper for our membershipprovider to allow com access
 * Perhaps COM API should be restricted to an admin user somehow? TODO
 * 
 * Maybe expose some more useful methods?
 * 
 */

using OdbcProvider;
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
        MembershipUser GetUser(string username, bool userIsOnline);
    }

    [Guid("DCF9ECC2-CE4C-46CA-8E8D-06556DEC8969")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class OdbcMembershipProviderWrapper : IOdbcMembershipProvider
    {
        string _connectionString;
        private readonly OdbcMembershipProvider _provider;
        Utils _Utils = new Utils();

        public OdbcMembershipProviderWrapper()
        {
            _provider = new OdbcMembershipProvider();
            _Utils.WriteDebug("OdbcMembershipProviderWrapper instance created.");
        }

        public void InitializeProvider()
        {
            try
            {
                var config = new NameValueCollection
                {
                    { "connectionString", _connectionString },
                    // Add other necessary configurations
                };

                _provider.ComInit(config);
                _Utils.WriteDebug("OdbcMembershipProvider initialized successfully.");
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error initializing OdbcMembershipProvider: {ex.Message}");
                throw; // Rethrow the exception to ensure the caller is aware of the failure
            }
        }

        public void SetConnectionString(string connectionString)
        {
            _connectionString = connectionString;
        }

        public bool ValidateUser(string username, string password)
        {
            InitializeProvider();
            try
            {
                if (_provider == null)
                {
                    _Utils.WriteDebug("Provider instance is not initialized.");
                    return false;
                }
                return _provider.ValidateUser(username, password);
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error in ValidateUser: {ex.Message}");
                return false;
            }
        }

        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            InitializeProvider();
            try
            {
                if (_provider == null)
                {
                    _Utils.WriteDebug("Provider instance is not initialized.");
                    return false;
                }
                return _provider.ChangePassword(username, oldPassword, newPassword);
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error in ChangePassword: {ex.Message}");
                return false;
            }
        }

        public bool UnlockUser(string userName)
        {
            InitializeProvider();
            try
            {
                if (_provider == null)
                {
                    _Utils.WriteDebug("Provider instance is not initialized.");
                    return false;
                }
                return _provider.UnlockUser(userName);
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error in UnlockUser: {ex.Message}");
                _Utils.WriteDebug(userName);
                return false;
            }
        }

        public MembershipUser GetUser(string username, bool userIsOnline)
        {
            InitializeProvider();
            try
            {
                if (_provider == null)
                {
                    _Utils.WriteDebug("Provider instance is not initialized.");
                    return null;
                }
                return _provider.GetUser(username, userIsOnline);
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"Error in GetUser: {ex.Message}");
                return null;
            }
        }
    }
}