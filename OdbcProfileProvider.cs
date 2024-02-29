using System;
using System.Configuration;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Diagnostics;
using System.Web.Profile;

namespace OdbcProvider
{
    public class OdbcProfileProvider : ProfileProvider
    {
        private string _connectionStringName;
        private string _connectionString;

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
                throw new ProviderException(
                  "No connection string was specified.\n");
            }
            _connectionString = ConfigurationManager.ConnectionStrings
              [_connectionStringName].ConnectionString;
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
                        using (OdbcCommand command = new OdbcCommand("SELECT propertyvalue FROM profiles WHERE username = ? AND propertyname = ?", connection))
                        {
                            command.Parameters.AddWithValue("username", username);
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
                Debug.WriteLine($"GetPropertyValues() Exception: {ex.Message}");
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
                        using (OdbcCommand command = new OdbcCommand("MERGE INTO profiles USING (VALUES(?, ?, ?)) AS NewValues (username, propertyname, propertyvalue) ON profiles.username = NewValues.username AND profiles.propertyname = NewValues.propertyname WHEN MATCHED THEN UPDATE SET propertyvalue = NewValues.propertyvalue WHEN NOT MATCHED THEN INSERT (username, propertyname, propertyvalue) VALUES (NewValues.username, NewValues.propertyname, NewValues.propertyvalue);", connection))
                        {
                            command.Parameters.AddWithValue("username", username);
                            command.Parameters.AddWithValue("propertyname", propValue.Name);
                            command.Parameters.AddWithValue("propertyvalue", propValue.PropertyValue ?? DBNull.Value);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetPropertyValues() Exception: {ex.Message}");
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
                string selectQuery = "SELECT username, lastactivitydate FROM profiles ORDER BY username OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

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
                                string username = reader["username"].ToString();
                                DateTime lastActivityDate = DateTime.Parse(reader["lastactivitydate"].ToString());

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                profiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllProfiles() Exception: {ex.Message}");
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
                string selectQuery = "SELECT username, lastactivitydate FROM profiles WHERE lastactivitydate <= ? ORDER BY username OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

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
                                string username = reader["username"].ToString();
                                DateTime lastActivityDate = DateTime.Parse(reader["lastactivitydate"].ToString());

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                inactiveProfiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllInactiveProfiles() Exception: {ex.Message}");
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
                Debug.WriteLine($"GetNumberOfInactiveProfiles() Exception: {ex.Message}");
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
                string query = "SELECT COUNT(*) FROM profiles WHERE lastactivitydate <= ? AND username LIKE ?";
                string selectQuery = "SELECT username, lastactivitydate FROM profiles WHERE lastactivitydate <= ? AND username LIKE ? ORDER BY username OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of inactive profiles matching the username pattern
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        countCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        countCommand.Parameters.AddWithValue("username", $"%{usernameToMatch}%");
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve inactive profiles matching the username pattern with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("lastactivitydate", userInactiveSinceDate);
                        selectCommand.Parameters.AddWithValue("username", $"%{usernameToMatch}%");
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader["username"].ToString();
                                DateTime lastActivityDate = DateTime.Parse(reader["lastactivitydate"].ToString());

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                inactiveProfiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindInactiveProfilesByUserName() Exception: {ex.Message}");
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
                string query = "SELECT COUNT(*) FROM profiles WHERE username LIKE ?";
                string selectQuery = "SELECT username, lastactivitydate FROM profiles WHERE username LIKE ? ORDER BY username OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of profiles matching the username pattern
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        countCommand.Parameters.AddWithValue("username", $"%{usernameToMatch}%");
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve profiles matching the username pattern with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("username", $"%{usernameToMatch}%");
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader["username"].ToString();
                                DateTime lastActivityDate = DateTime.Parse(reader["lastactivitydate"].ToString());

                                ProfileInfo profile = new ProfileInfo(username, false, lastActivityDate, lastActivityDate, 0);
                                profiles.Add(profile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindProfilesByUserName() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while finding profiles by username.");
            }

            return profiles;
        }

        public override int DeleteProfiles(ProfileInfoCollection profiles)
        {
            int deletedCount = 0;

            try
            {
                string deleteQuery = "DELETE FROM profiles WHERE username = ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    foreach (ProfileInfo profile in profiles)
                    {
                        using (OdbcCommand deleteCommand = new OdbcCommand(deleteQuery, connection))
                        {
                            deleteCommand.Parameters.AddWithValue("username", profile.UserName);
                            deletedCount += deleteCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteProfiles() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while deleting profiles.");
            }

            return deletedCount;
        }


        public override int DeleteProfiles(string[] usernames)
        {
            int deletedCount = 0;

            try
            {
                string deleteQuery = "DELETE FROM profiles WHERE username = ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    foreach (string username in usernames)
                    {
                        using (OdbcCommand deleteCommand = new OdbcCommand(deleteQuery, connection))
                        {
                            deleteCommand.Parameters.AddWithValue("username", username);
                            deletedCount += deleteCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteProfiles(string[]) Exception: {ex.Message}");
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
                Debug.WriteLine($"DeleteInactiveProfiles() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while deleting inactive profiles.");
            }

            return deletedCount;
        }

    }
}

