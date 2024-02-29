using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Configuration;
using System.Data.Odbc;
using System.Web.Security;
using System.Diagnostics;

namespace OdbcProvider
{
    public class OdbcRoleProvider : RoleProvider
    {
        private string _connectionStringName;
        private string _connectionString;
        private Utils _Utils;

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
                name = "OdbcRoleProvider";
            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Odbc role provider");
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
        }

        /* ---------------------------------------- */

        public override bool IsUserInRole(string username, string roleName)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(roleName))
                throw new ArgumentNullException();

            bool userInRole = false;
            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM user_group ug JOIN `groups` g ON ug.group_id = g.group_id JOIN `users` u ON ug.user_id = u.user_id WHERE u.username = ? AND g.group_name = ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("username", username);
                        command.Parameters.AddWithValue("roleName", roleName);
                        int count = Convert.ToInt32(command.ExecuteScalar());
                        userInRole = count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IsUserInRole() Exception: {ex.Message}");
            }

            return userInRole;
        }

        /* ---------------------------------------- */

        public override string[] GetRolesForUser(string username)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentNullException();

            string[] roles = new string[0];
            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT g.group_name FROM user_group ug JOIN `groups` g ON ug.group_id = g.group_id JOIN `users` u ON ug.user_id = u.user_id WHERE u.username = ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("username", username);
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            var roleList = new List<string>();
                            while (reader.Read())
                            {
                                roleList.Add(reader["group_name"].ToString());
                            }
                            roles = roleList.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRolesForUser() Exception: {ex.Message}");
            }

            return roles;
        }

        /* ---------------------------------------- */

        public override string[] GetUsersInRole(string roleName)
        {
            if (string.IsNullOrEmpty(roleName))
                throw new ArgumentException();

            List<string> usersInRole = new List<string>();
            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT u.username FROM user_group ug JOIN `groups` g ON ug.group_id = g.group_id JOIN `users` u ON ug.user_id = u.user_id WHERE g.group_name = ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("roleName", roleName);
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader["username"].ToString();
                                usersInRole.Add(username);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUsersInRole() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while retrieving users in role.");
            }

            return usersInRole.ToArray();
        }


        /* ---------------------------------------- */

        public override string[] GetAllRoles()
        {
            List<string> allRoles = new List<string>();
            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT group_name FROM `groups`";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string roleName = reader["group_name"].ToString();
                                allRoles.Add(roleName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllRoles() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while retrieving all roles.");
            }

            return allRoles.ToArray();
        }


        /* ---------------------------------------- */

        public override bool RoleExists(string roleName)
        {
            if (roleName == null)
                throw new ArgumentNullException();
            if (roleName == String.Empty)
                throw new ArgumentException();
            try
            {
                string userQueryString = $"SELECT * FROM `groups` WHERE `group_name`={roleName}";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    OdbcCommand cmd = new OdbcCommand(userQueryString, connection);
                    OdbcDataReader reader = cmd.ExecuteReader();
                    if(reader.Read())
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteRole() Exception: {ex.Message}");
            }
            return false;
        }

        /* ---------------------------------------- */

        public override void CreateRole(string roleName)
        {
            try
            {
                string userQueryString = $"INSERT INTO `groups` (`group_name`) VALUES ('{roleName}')";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    OdbcCommand cmd = new OdbcCommand(userQueryString, connection);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateRole() Exception: {ex.Message}");
            }
        }
        /* ---------------------------------------- */

        public override bool DeleteRole(
          string roleName, bool throwOnPopulatedRole)
        {
            try
            {
                string userQueryString = $"DELETE FROM `groups` WHERE `group_name`={roleName}";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    OdbcCommand cmd = new OdbcCommand(userQueryString, connection);
                    if(cmd.ExecuteNonQuery() > 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteRole() Exception: {ex.Message}");
            }
            return false;
        }

        /* ---------------------------------------- */

        public override void AddUsersToRoles(string[] usernames, string[] roleNames)
        {
            if (usernames == null || roleNames == null || usernames.Length == 0 || roleNames.Length == 0)
                throw new ArgumentException("Usernames and role names must be provided.");

            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    foreach (string username in usernames)
                    {
                        foreach (string roleName in roleNames)
                        {
                            string userIdQuery = "SELECT user_id FROM `users` WHERE username = ?";
                            string roleIdQuery = "SELECT group_id FROM `groups` WHERE group_name = ?";
                            string addUserRoleQuery = "INSERT INTO user_group (user_id, group_id) VALUES (?, ?)";

                            // Get user ID
                            int userId;
                            using (OdbcCommand userIdCommand = new OdbcCommand(userIdQuery, connection))
                            {
                                userIdCommand.Parameters.AddWithValue("username", username);
                                userId = Convert.ToInt32(userIdCommand.ExecuteScalar());
                            }

                            // Get role ID
                            int roleId;
                            using (OdbcCommand roleIdCommand = new OdbcCommand(roleIdQuery, connection))
                            {
                                roleIdCommand.Parameters.AddWithValue("roleName", roleName);
                                roleId = Convert.ToInt32(roleIdCommand.ExecuteScalar());
                            }

                            // Add user to role
                            using (OdbcCommand addUserRoleCommand = new OdbcCommand(addUserRoleQuery, connection))
                            {
                                addUserRoleCommand.Parameters.AddWithValue("userId", userId);
                                addUserRoleCommand.Parameters.AddWithValue("roleId", roleId);
                                addUserRoleCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddUsersToRoles() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while adding users to roles.");
            }
        }


        /* ---------------------------------------- */

        public override string[] FindUsersInRole(string roleName, string usernameToMatch)
        {
            if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(usernameToMatch))
                throw new ArgumentException("Role name and username to match must be provided.");

            List<string> matchedUsernames = new List<string>();

            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    string query = "SELECT u.username FROM `users` u INNER JOIN user_group ug ON u.user_id = ug.user_id INNER JOIN `groups` g ON ug.group_id = g.group_id WHERE g.group_name = ? AND u.username LIKE ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("roleName", roleName);
                        command.Parameters.AddWithValue("usernameToMatch", $"%{usernameToMatch}%");

                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader["username"].ToString();
                                matchedUsernames.Add(username);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindUsersInRole() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while finding users in role.");
            }

            return matchedUsernames.ToArray();
        }


        /* ---------------------------------------- */

        public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
        {
            if (usernames == null || roleNames == null || usernames.Length == 0 || roleNames.Length == 0)
                throw new ArgumentException("Usernames and role names must be provided.");

            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    foreach (string username in usernames)
                    {
                        foreach (string roleName in roleNames)
                        {
                            string userIdQuery = "SELECT user_id FROM `users` WHERE username = ?";
                            string roleIdQuery = "SELECT group_id FROM `groups` WHERE group_name = ?";
                            string deleteUserRoleQuery = "DELETE FROM user_group WHERE user_id = ? AND group_id = ?";

                            // Get user ID
                            int userId;
                            using (OdbcCommand userIdCommand = new OdbcCommand(userIdQuery, connection))
                            {
                                userIdCommand.Parameters.AddWithValue("username", username);
                                userId = Convert.ToInt32(userIdCommand.ExecuteScalar());
                            }

                            // Get role ID
                            int roleId;
                            using (OdbcCommand roleIdCommand = new OdbcCommand(roleIdQuery, connection))
                            {
                                roleIdCommand.Parameters.AddWithValue("roleName", roleName);
                                roleId = Convert.ToInt32(roleIdCommand.ExecuteScalar());
                            }

                            // Remove user from role
                            using (OdbcCommand deleteUserRoleCommand = new OdbcCommand(deleteUserRoleQuery, connection))
                            {
                                deleteUserRoleCommand.Parameters.AddWithValue("userId", userId);
                                deleteUserRoleCommand.Parameters.AddWithValue("roleId", roleId);
                                deleteUserRoleCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemoveUsersFromRoles() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while removing users from roles.");
            }
        }
    }
}
