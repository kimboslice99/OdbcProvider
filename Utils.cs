using System;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Security.Cryptography;
using System.Text;

namespace OdbcProvider
{
    internal class Utils
    {
        internal bool IsExistingUsername(string username, string connectionString)
        {
            try
            {
                string query = $"SELECT COUNT(*) FROM users WHERE username = '{username}'";

                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        connection.Open();
                        int count = (int)command.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Console.WriteLine($"Error checking existing username: {ex.Message}");
                return false;
            }
        }

        internal void ExecuteNonQuery(string query, string connectionString)
        {
            try
            {
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking existing username: {ex.Message}");
                throw new Exception();
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
                System.Diagnostics.Debug.WriteLine($"Error getting last inserted user ID: {ex.Message}");
                throw new Exception();
            }
        }


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
            string userQueryString = $"SELECT `user_id` FROM `users` WHERE `username`={username}";
            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {
                connection.Open();
                OdbcCommand userCommand = new OdbcCommand(userQueryString, connection);
                return Convert.ToInt32(userCommand.ExecuteScalar());

            }
        }
    }
}
