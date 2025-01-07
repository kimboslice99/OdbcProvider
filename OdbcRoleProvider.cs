using System;
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
                throw new ProviderException("No connection string was specified.");
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
                    string query = "SELECT COUNT(*) FROM user_roles ug JOIN roles g ON ug.role_id = g.role_id JOIN users u ON ug.user_id = u.user_id WHERE u.user_name = ? AND g.role_name = ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("user_name", username);
                        command.Parameters.AddWithValue("role_name", roleName);
                        int count = Convert.ToInt32(command.ExecuteScalar());
                        userInRole = count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"IsUserInRole(): {ex.Message}");
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
                    string query = "SELECT g.role_name FROM user_roles ug JOIN roles g ON ug.role_id = g.role_id JOIN users u ON ug.user_id = u.user_id WHERE u.user_name = ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("user_name", username);
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            var roleList = new List<string>();
                            while (reader.Read())
                            {
                                roleList.Add(reader["role_name"].ToString());
                            }
                            roles = roleList.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetRolesForUser() {ex.Message}");
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
                    string query = "SELECT u.user_name FROM user_roles ug JOIN roles g ON ug.role_id = g.role_id JOIN users u ON ug.user_id = u.user_id WHERE g.role_name = ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("role_name", roleName);
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader["user_name"].ToString();
                                usersInRole.Add(username);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetUsersInRole() {ex.Message}");
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
                    string query = "SELECT role_name FROM roles";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string roleName = Convert.ToString(reader["role_name"]);
                                allRoles.Add(roleName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetAllRoles() {ex.Message}");
                throw new ProviderException("An error occurred while retrieving all roles.");
            }

            return allRoles.ToArray();
        }

        /* ---------------------------------------- */

        public override bool RoleExists(string roleName)
        {
            try
            {
                if (roleName == null)
                    throw new ArgumentNullException();
                if (roleName == String.Empty)
                    throw new ArgumentException();

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    string query = $"SELECT * FROM roles WHERE role_name = ?";
                    OdbcCommand cmd = new OdbcCommand(query, connection);
                    cmd.Parameters.AddWithValue("role_name", roleName);
                    OdbcDataReader reader = cmd.ExecuteReader();
                    if(reader.Read())
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"RoleExists() {ex.Message}");
            }
            return false;
        }

        /* ---------------------------------------- */

        public override void CreateRole(string roleName)
        {
            try
            {
                string query = $"INSERT INTO roles (role_name) VALUES (?)";
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "role_name", roleName }
                };
                int result = _Utils.ExecutePreparedNonQuery(query, _connectionString, parameters);
                if(result == 0)
                {
                    throw new ProviderException($"Failed to create {roleName} role");
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"CreateRole() {ex.Message}");
            }
        }

        /* ---------------------------------------- */

        public override bool DeleteRole(
          string roleName, bool throwOnPopulatedRole)
        {
            try
            {
                string query = $"DELETE FROM roles WHERE role_name = ?";
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "role_name", roleName }
                };
                int result = _Utils.ExecutePreparedNonQuery(query, _connectionString, parameters);
                if(result == 0)
                {
                    throw new ProviderException($"Failed to delete {roleName} role");
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"DeleteRole() {ex.Message}");
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
                            string userIdQuery = "SELECT user_id FROM users WHERE user_name = ?";
                            string roleIdQuery = "SELECT role_id FROM roles WHERE role_name = ?";
                            string addUserRoleQuery = "INSERT INTO user_roles (user_id, role_id) VALUES (?, ?)";

                            // Get user ID
                            int userId;
                            using (OdbcCommand userIdCommand = new OdbcCommand(userIdQuery, connection))
                            {
                                userIdCommand.Parameters.AddWithValue("user_name", username);
                                userId = Convert.ToInt32(userIdCommand.ExecuteScalar());
                            }

                            // Get role ID
                            int roleId;
                            using (OdbcCommand roleIdCommand = new OdbcCommand(roleIdQuery, connection))
                            {
                                roleIdCommand.Parameters.AddWithValue("role_name", roleName);
                                roleId = Convert.ToInt32(roleIdCommand.ExecuteScalar());
                            }

                            // Add user to role
                            using (OdbcCommand addUserRoleCommand = new OdbcCommand(addUserRoleQuery, connection))
                            {
                                addUserRoleCommand.Parameters.AddWithValue("user_id", userId);
                                addUserRoleCommand.Parameters.AddWithValue("role_id", roleId);
                                addUserRoleCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"AddUsersToRoles() {ex.Message}");
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

                    string query = "SELECT u.user_name FROM users u INNER JOIN user_roles ug ON u.user_id = ug.user_id INNER JOIN roles g ON ug.role_id = g.role_id WHERE g.role_name = ? AND u.user_name LIKE ?";
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("role_name", roleName);
                        command.Parameters.AddWithValue("usernameToMatch", $"%{usernameToMatch}%");

                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader["user_name"].ToString();
                                matchedUsernames.Add(username);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"FindUsersInRole() {ex.Message}");
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
                            string userIdQuery = "SELECT user_id FROM users WHERE user_name = ?";
                            string roleIdQuery = "SELECT role_id FROM roles WHERE role_name = ?";
                            string deleteUserRoleQuery = "DELETE FROM user_roles WHERE user_id = ? AND role_id = ?";

                            // Get user ID
                            int userId;
                            using (OdbcCommand userIdCommand = new OdbcCommand(userIdQuery, connection))
                            {
                                userIdCommand.Parameters.AddWithValue("user_name", username);
                                userId = Convert.ToInt32(userIdCommand.ExecuteScalar());
                            }

                            // Get role ID
                            int roleId;
                            using (OdbcCommand roleIdCommand = new OdbcCommand(roleIdQuery, connection))
                            {
                                roleIdCommand.Parameters.AddWithValue("role_name", roleName);
                                roleId = Convert.ToInt32(roleIdCommand.ExecuteScalar());
                            }

                            // Remove user from role
                            using (OdbcCommand deleteUserRoleCommand = new OdbcCommand(deleteUserRoleQuery, connection))
                            {
                                deleteUserRoleCommand.Parameters.AddWithValue("user_id", userId);
                                deleteUserRoleCommand.Parameters.AddWithValue("role_id", roleId);
                                deleteUserRoleCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"RemoveUsersFromRoles() {ex.Message}");
                throw new ProviderException("An error occurred while removing users from roles.");
            }
        }
    }
}
