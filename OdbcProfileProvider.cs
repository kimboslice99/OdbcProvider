using System;
using System.Configuration;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Web.Profile;

namespace OdbcProvider
{
    public class OdbcProfileProvider : ProfileProvider
    {
        private string _connectionStringName;
        private string _connectionString;
        private Utils _Utils;

        public override string ApplicationName { get; set; }

        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrEmpty(name))
                name = "OdbcProfileProvider";

            if (string.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Odbc profile provider");
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

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (collection == null || collection.Count < 1)
                return new SettingsPropertyValueCollection();

            string username = (string)context["UserName"];
            bool isAuthenticated = (bool)context["IsAuthenticated"];

            var properties = new SettingsPropertyValueCollection();

            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    foreach (SettingsProperty prop in collection)
                    {
                        using (OdbcCommand command = new OdbcCommand("SELECT propertyvalue FROM profiles WHERE user_name = ? AND propertyname = ?", connection))
                        {
                            command.Parameters.AddWithValue("user_name", username);
                            command.Parameters.AddWithValue("propertyname", prop.Name);

                            object value = command.ExecuteScalar();
                            var propValue = new SettingsPropertyValue(prop)
                            {
                                PropertyValue = value ?? prop.DefaultValue,
                                IsDirty = false
                            };
                            properties.Add(propValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetPropertyValues() {ex.Message}");
                throw new ProviderException("An error occurred while retrieving property values.");
            }

            return properties;
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (collection == null || collection.Count < 1)
                return;

            string username = (string)context["UserName"];

            try
            {
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    foreach (SettingsPropertyValue propValue in collection)
                    {
                        using (OdbcCommand command = new OdbcCommand("MERGE INTO profiles USING (VALUES(?, ?, ?)) AS NewValues (user_name, propertyname, propertyvalue) ON profiles.user_name = NewValues.user_name AND profiles.propertyname = NewValues.propertyname WHEN MATCHED THEN UPDATE SET propertyvalue = NewValues.propertyvalue WHEN NOT MATCHED THEN INSERT (user_name, propertyname, propertyvalue) VALUES (NewValues.user_name, NewValues.propertyname, NewValues.propertyvalue);", connection))
                        {
                            command.Parameters.AddWithValue("user_name", username);
                            command.Parameters.AddWithValue("propertyname", propValue.Name);
                            command.Parameters.AddWithValue("propertyvalue", propValue.PropertyValue ?? DBNull.Value);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"SetPropertyValues() {ex.Message}");
                throw new ProviderException("An error occurred while setting property values.");
            }
        }


        public override ProfileInfoCollection GetAllProfiles(ProfileAuthenticationOption authenticationOption, int pageIndex, int pageSize, out int totalRecords)
        {
            ProfileInfoCollection profiles = new ProfileInfoCollection();
            totalRecords = 0;

            try
            {
                string query = "SELECT COUNT(*) FROM profiles";
                string selectQuery = "SELECT user_name, lastactivitydate FROM profiles ORDER BY user_name OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of profiles
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve profiles with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = Convert.ToString(reader["user_name"]);
                                DateTime lastActivityDate = Convert.ToDateTime(reader["lastactivitydate"]);

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                profiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetAllProfiles() {ex.Message}");
                throw new ProviderException("An error occurred while retrieving all profiles.");
            }

            return profiles;
        }


        public override ProfileInfoCollection GetAllInactiveProfiles(ProfileAuthenticationOption authenticationOption, DateTime userInactiveSinceDate, int pageIndex, int pageSize, out int totalRecords)
        {
            ProfileInfoCollection inactiveProfiles = new ProfileInfoCollection();
            totalRecords = 0;

            try
            {
                string query = "SELECT COUNT(*) FROM profiles WHERE lastactivitydate <= ?";
                string selectQuery = "SELECT user_name, lastactivitydate FROM profiles WHERE lastactivitydate <= ? ORDER BY user_name OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of inactive profiles
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        countCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve inactive profiles with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = Convert.ToString(reader["user_name"]);
                                DateTime lastActivityDate = Convert.ToDateTime(reader["lastactivitydate"]);

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                inactiveProfiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetAllInactiveProfiles() {ex.Message}");
                throw new ProviderException("An error occurred while retrieving all inactive profiles.");
            }

            return inactiveProfiles;
        }

        public override int GetNumberOfInactiveProfiles(ProfileAuthenticationOption authenticationOption, DateTime userInactiveSinceDate)
        {
            int totalInactiveProfiles = 0;

            try
            {
                string query = "SELECT COUNT(*) FROM profiles WHERE lastactivitydate <= ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of inactive profiles
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        countCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        totalInactiveProfiles = Convert.ToInt32(countCommand.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetNumberOfInactiveProfiles() {ex.Message}");
                throw new ProviderException("An error occurred while retrieving the number of inactive profiles.");
            }

            return totalInactiveProfiles;
        }

        public override ProfileInfoCollection FindInactiveProfilesByUserName(ProfileAuthenticationOption authenticationOption, string usernameToMatch, DateTime userInactiveSinceDate, int pageIndex, int pageSize, out int totalRecords)
        {
            ProfileInfoCollection inactiveProfiles = new ProfileInfoCollection();
            totalRecords = 0;

            try
            {
                string query = "SELECT COUNT(*) FROM profiles WHERE lastactivitydate <= ? AND user_name LIKE ?";
                string selectQuery = "SELECT user_name, lastactivitydate FROM profiles WHERE lastactivitydate <= ? AND user_name LIKE ? ORDER BY user_name OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of inactive profiles matching the username pattern
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        countCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        countCommand.Parameters.AddWithValue("user_name", $"%{usernameToMatch}%");
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve inactive profiles matching the username pattern with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        selectCommand.Parameters.AddWithValue("user_name", $"%{usernameToMatch}%");
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = Convert.ToString(reader["user_name"]);
                                DateTime lastActivityDate = Convert.ToDateTime(reader["lastactivitydate"]);

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                inactiveProfiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"FindInactiveProfilesByUserName() {ex.Message}");
                throw new ProviderException("An error occurred while finding inactive profiles by username.");
            }

            return inactiveProfiles;
        }

        public override ProfileInfoCollection FindProfilesByUserName(ProfileAuthenticationOption authenticationOption, string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            ProfileInfoCollection profiles = new ProfileInfoCollection();
            totalRecords = 0;

            try
            {
                string query = "SELECT COUNT(*) FROM profiles WHERE user_name LIKE ?";
                string selectQuery = "SELECT user_name, lastactivitydate FROM profiles WHERE user_name LIKE ? ORDER BY user_name OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of profiles matching the username pattern
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        countCommand.Parameters.AddWithValue("user_name", $"%{usernameToMatch}%");
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve profiles matching the username pattern with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("user_name", $"%{usernameToMatch}%");
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = Convert.ToString(reader["user_name"]);
                                DateTime lastActivityDate = Convert.ToDateTime(reader["lastactivitydate"]);

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                profiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"FindProfilesByUserName() {ex.Message}");
                throw new ProviderException("An error occurred while finding profiles by username.");
            }

            return profiles;
        }

        public override int DeleteProfiles(ProfileInfoCollection profiles)
        {
            int deletedCount = 0;

            try
            {
                string deleteQuery = "DELETE FROM profiles WHERE user_name = ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    foreach (ProfileInfo profile in profiles)
                    {
                        using (OdbcCommand deleteCommand = new OdbcCommand(deleteQuery, connection))
                        {
                            deleteCommand.Parameters.AddWithValue("user_name", profile.UserName);
                            deletedCount += deleteCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"DeleteProfiles() {ex.Message}");
                throw new ProviderException("An error occurred while deleting profiles.");
            }

            return deletedCount;
        }


        public override int DeleteProfiles(string[] usernames)
        {
            int deletedCount = 0;

            try
            {
                string deleteQuery = "DELETE FROM profiles WHERE user_name = ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    foreach (string username in usernames)
                    {
                        using (OdbcCommand deleteCommand = new OdbcCommand(deleteQuery, connection))
                        {
                            deleteCommand.Parameters.AddWithValue("user_name", username);
                            deletedCount += deleteCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"DeleteProfiles(string[]) {ex.Message}");
                throw new ProviderException("An error occurred while deleting profiles.");
            }

            return deletedCount;
        }

        public override int DeleteInactiveProfiles(ProfileAuthenticationOption authenticationOption, DateTime userInactiveSinceDate)
        {
            int deletedCount = 0;

            try
            {
                string deleteQuery = "DELETE FROM profiles WHERE lastactivitydate <= ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Delete inactive profiles based on LastActivityDate
                    using (OdbcCommand deleteCommand = new OdbcCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        deletedCount = deleteCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"DeleteInactiveProfiles() {ex.Message}");
                throw new ProviderException("An error occurred while deleting inactive profiles.");
            }

            return deletedCount;
        }
    }
}

