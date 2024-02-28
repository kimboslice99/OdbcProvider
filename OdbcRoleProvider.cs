using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Configuration;
using System.Data.Odbc;
using System.Web.Security;

namespace OdbcProvider
{
    public class OdbcRoleProvider : RoleProvider
    {
        private string _connectionStringName;
        private string _connectionString;
        private Utils _Utils;
        private Dictionary<string, string[]> _UsersAndRoles =
          new Dictionary<string, string[]>(
            16, StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<string, string[]> _RolesAndUsers =
          new Dictionary<string, string[]>(
            16, StringComparer.InvariantCultureIgnoreCase);

        /* ---------------------------------------- */
        // RoleProvider properties
        /* ---------------------------------------- */

        public override string ApplicationName
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */
        // RoleProvider methods
        /* ---------------------------------------- */

        public override void Initialize(
          string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            if (String.IsNullOrEmpty(name))
                name = "ReadOnlyPhpBBRoleProvider";
            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Read-only phpBB role provider");
            }
            base.Initialize(name, config);
            _connectionStringName = config["connectionStringName"];
            if (String.IsNullOrEmpty(_connectionStringName))
            {
                throw new ProviderException(
                  "No connection string was specified.\n");
            }
            _connectionString = ConfigurationManager.ConnectionStrings
              [_connectionStringName].ConnectionString;
            _Utils = new Utils();
            ReadRoleDataStore();
        }

        /* ---------------------------------------- */

        public override bool IsUserInRole(
          string username, string roleName)
        {
            if (username == null || roleName == null)
                throw new ArgumentNullException();
            if (username == String.Empty || roleName == String.Empty)
                throw new ArgumentException();
            if (!_UsersAndRoles.ContainsKey(username))
                throw new ProviderException("Invalid user name");
            if (!_RolesAndUsers.ContainsKey(roleName))
                throw new ProviderException("Invalid role name");
            string[] roles = _UsersAndRoles[username];
            foreach (string role in roles)
            {
                if (String.Compare(role, roleName, true) == 0)
                    return true;
            }
            return false;
        }

        /* ---------------------------------------- */

        public override string[] GetRolesForUser(string username)
        {
            if (username == null)
                throw new ArgumentNullException();
            if (username == String.Empty)
                throw new ArgumentException();
            string[] roles;
            if (!_UsersAndRoles.TryGetValue(username, out roles))
                throw new ProviderException("Invalid user name");
            return roles;
        }

        /* ---------------------------------------- */

        public override string[] GetUsersInRole(string roleName)
        {
            if (roleName == null)
                throw new ArgumentNullException();
            if (roleName == string.Empty)
                throw new ArgumentException();
            string[] users;
            if (!_RolesAndUsers.TryGetValue(roleName, out users))
                throw new ProviderException("Invalid role name");
            return users;
        }

        /* ---------------------------------------- */

        public override string[] GetAllRoles()
        {
            int i = 0;
            string[] roles = new string[_RolesAndUsers.Count];
            foreach (KeyValuePair<string, string[]> pair in _RolesAndUsers)
                roles[i++] = pair.Key;
            return roles;
        }

        /* ---------------------------------------- */

        public override bool RoleExists(string roleName)
        {
            if (roleName == null)
                throw new ArgumentNullException();
            if (roleName == String.Empty)
                throw new ArgumentException();
            return _RolesAndUsers.ContainsKey(roleName);
        }

        /* ---------------------------------------- */

        public override void CreateRole(string roleName)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool DeleteRole(
          string roleName, bool throwOnPopulatedRole)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override void AddUsersToRoles(
          string[] usernames, string[] roleNames)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string[] FindUsersInRole(
          string roleName, string usernameToMatch)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override void RemoveUsersFromRoles(
          string[] usernames, string[] roleNames)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */
        // RoleProvider helper method
        /* ---------------------------------------- */

        private void ReadRoleDataStore()
        {
            lock (this)
            {
                try
                {
                    string userQueryString = "SELECT * FROM users";
                    using (OdbcConnection connection = new OdbcConnection(_connectionString))
                    {
                        connection.Open();
                        OdbcCommand userCommand = new OdbcCommand(userQueryString, connection);
                        OdbcDataReader userReader = userCommand.ExecuteReader();
                        while (userReader.Read())
                        {
                            string user = userReader["username"].ToString();
                            string userid = userReader["user_id"].ToString();

                            string groupQueryString =
                                "SELECT groups.group_name " +
                                "FROM user_group " +
                                "INNER JOIN groups ON user_group.group_id = groups.group_id " +
                                "WHERE user_group.user_id = " + userid;

                            OdbcCommand groupCommand = new OdbcCommand(groupQueryString, connection);
                            OdbcDataReader groupReader = groupCommand.ExecuteReader();

                            if (!groupReader.HasRows)
                            {
                                _UsersAndRoles.Add(user, new string[0]);
                            }
                            else
                            {
                                ArrayList roleList = new ArrayList();
                                while (groupReader.Read())
                                {
                                    roleList.Add(groupReader["group_name"].ToString());
                                }
                                string[] roles = (string[])roleList.ToArray(typeof(string));
                                _UsersAndRoles.Add(user, roles);
                                foreach (string role in roles)
                                {
                                    string[] users1;
                                    if (_RolesAndUsers.TryGetValue(role, out users1))
                                    {
                                        string[] users2 = new string[users1.Length + 1];
                                        users1.CopyTo(users2, 0);
                                        users2[users1.Length] = user;
                                        _RolesAndUsers.Remove(role);
                                        _RolesAndUsers.Add(role, users2);
                                    }
                                    else
                                        _RolesAndUsers.Add(role, new string[] { user });
                                }
                            }
                            groupReader.Close();
                        }
                        userReader.Close();
                    }
                }
                catch (Exception ex)
                {
                    throw new ProviderException("Error: " + ex.Message);
                }
            }
        }
    }
}
