using System;
using System.Collections.Generic;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Diagnostics;

namespace OdbcProvider
{
    internal class Utils
    {
        internal void WriteDebug(string msg)
        {
#if DEBUG
            msg = $"{DateTime.Now} [ODBCProvider]: {msg}";
            Debug.WriteLine(msg);
#endif
        }
        /// <summary>
        /// Checks if a username exists
        /// </summary>
        /// <param name="username">The username to check</param>
        /// <param name="connectionString">The ODBC connection string</param>
        /// <returns></returns>
        internal bool IsExistingUsername(string username, string connectionString)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM users WHERE user_name = ?";
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    command.Parameters.AddWithValue("user_name", username);
                    connection.Open();
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"IsExistingUsername() {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Executes a query without returning the response
        /// </summary>
        /// <param name="query">The query to execute</param>
        /// <param name="connectionString">ODBC connection string</param>
        /// <exception cref="Exception"></exception>
        internal int ExecuteNonQuery(string query, string connectionString)
        {
            try
            {
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    connection.Open();
                    return command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"ExecuteNonQuery() {ex.Message}");
                throw new InvalidOperationException(ex.Message, ex.InnerException);
            }
        }

        internal int ExecutePreparedNonQuery(string query, string connectionString, Dictionary<string, object> parameters = null)
        {
            try
            {
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    connection.Open();
                    foreach (var item in parameters)
                    {
                        if(null != item.Key && null != item.Value)
                        {
                            command.Parameters.AddWithValue(item.Key, item.Value);
                        }
                    }
                    return command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"ExecutePreparedNonQuery() {ex.Message}");
                WriteDebug($"{ex.Source}");
                WriteDebug($"{ex.StackTrace}");
                throw new InvalidOperationException(ex.Message, ex.InnerException);
            }
        }

        internal int GetLastInsertedUserId(string connectionString)
        {
            try
            {
                string query = "SELECT LAST_INSERT_ID()";

                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        connection.Open();
                        object result = command.ExecuteScalar();
                        // Convert the result to an integer
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"GetLastInsertedUserId(): {ex.Message}");
                throw new Exception(ex.Message, ex.InnerException);
            }
        }

        /// <summary>
        /// BCrypt hash a passwor
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <exception cref="ProviderException"></exception>
        internal string PasswordHash(string password)
        {
            try
            {
                return BCrypt.Net.BCrypt.HashPassword(password);
            }
            catch (Exception ex)
            {
                throw new ProviderException("Error: " + ex.Message);
            }
        }

        /// <summary>
        /// BCrypt verify a password
        /// </summary>
        /// <param name="password">The input to test</param>
        /// <param name="hash">The hash to test it against</param>
        /// <returns></returns>
        internal bool PasswordVerify(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }

        /* ---------------------------------------- */

        internal DateTime ConvertDate(string offset)
        {
            DateTime dateTime = new DateTime(1970, 1, 1);
            if (!(String.IsNullOrEmpty(offset)))
            {
                try
                {
                    dateTime = dateTime.AddSeconds(Convert.ToDouble(offset));
                }
                catch (Exception ex)
                {
                    throw new ProviderException("Error: " + ex.Message);
                }
            }
            return dateTime;
        }

        internal int GetUserId(string username, string connectionString)
        {
            string query = $"SELECT user_id FROM users WHERE user_name = ?";
            using (OdbcConnection connection = new OdbcConnection(connectionString))
            using (OdbcCommand command = new OdbcCommand(query, connection))
            {
                connection.Open();
                command.Parameters.AddWithValue("user_name", username);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        static string GetDatabaseName(OdbcConnection connection)
        {
            // Get database metadata
            var metaData = connection.GetSchema("DataSourceInformation");
            string productName = metaData.Rows[0]["DataSourceProductName"].ToString();
            return productName;
        }

        internal void DatabaseInit(string connectionString)
        {
            try
            {
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    connection.Open();
                    string tableName = GetDatabaseName(connection);
                    WriteDebug(tableName);
                    switch(tableName)
                    {
                        case "MariaDB":
                            MariaDBExecuteInit(connectionString);
                            break;
                        case "MySQL":
                            MariaDBExecuteInit(connectionString);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                WriteDebug(e.Message);
                throw new ProviderException($"DatabaseInit() {e.Message}");
            }
        }

        internal void MariaDBExecuteInit(string connectionString)
        {
            string queryRoles = @"CREATE TABLE IF NOT EXISTS `roles` (
                                  `role_id` int(11) NOT NULL AUTO_INCREMENT,
                                  `role_name` varchar(50) DEFAULT NULL,
                                  PRIMARY KEY (`role_id`)
                                ) AUTO_INCREMENT=1;";

            string queryUsers = @"CREATE TABLE IF NOT EXISTS `users` (
                                  `user_id` int(11) NOT NULL AUTO_INCREMENT,
                                  `user_name` varchar(50) NOT NULL,
                                  `user_password` BINARY(60) NOT NULL,
                                  `user_email` varchar(255) DEFAULT NULL,
                                  `user_regdate` DATETIME DEFAULT NULL,
                                  `user_last_login` DATETIME DEFAULT NULL,
                                  `user_last_activity` DATETIME DEFAULT NULL,
                                  `user_last_password_changed` DATETIME DEFAULT NULL,
                                  `user_last_lockout` DATETIME DEFAULT NULL,
                                  `user_password_question` TEXT DEFAULT NULL,
                                  `user_password_answer` TEXT DEFAULT NULL,
                                  `user_approved` int(11) DEFAULT NULL,
                                  `user_locked` int(11) DEFAULT NULL,
                                  PRIMARY KEY (`user_id`)
                                ) AUTO_INCREMENT=1;";

            string queryUserRoles = @"CREATE TABLE IF NOT EXISTS `user_roles` (
                                  `user_id` int(11) DEFAULT NULL,
                                  `role_id` int(11) DEFAULT NULL
                                );";

            string queryProfiles = @"CREATE TABLE IF NOT EXISTS `profiles` (
                                    `user_name` VARCHAR(255) NOT NULL,
                                    lastactivitydate DATETIME,
                                    propertyname VARCHAR(255) NOT NULL,
                                    propertyvalue TEXT,
                                    CONSTRAINT PK_profiles PRIMARY KEY (`user_name`, propertyname)
                                );";

            ExecuteNonQuery(queryRoles, connectionString);
            ExecuteNonQuery(queryUsers, connectionString);
            ExecuteNonQuery(queryUserRoles, connectionString);
            ExecuteNonQuery(queryProfiles, connectionString);
        }
    }
}
