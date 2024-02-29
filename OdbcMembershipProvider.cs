using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Diagnostics;
using System.Web.Security;

namespace OdbcProvider
{
    public class OdbcMembershipProvider : MembershipProvider
    {
        private string _connectionStringName;
        private string _connectionString;
        private Utils _Utils;

        /* ---------------------------------------- */
        // MembershipProvider Properties
        /* ---------------------------------------- */

        public override string ApplicationName
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override bool EnablePasswordRetrieval
        {
            get { return false; }
        }

        /* ---------------------------------------- */

        public override bool EnablePasswordReset
        {
            get { return false; }
        }

        /* ---------------------------------------- */

        public override int MaxInvalidPasswordAttempts
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override int MinRequiredNonAlphanumericCharacters
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override int MinRequiredPasswordLength
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override int PasswordAttemptWindow
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override MembershipPasswordFormat PasswordFormat
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override string PasswordStrengthRegularExpression
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override bool RequiresQuestionAndAnswer
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override bool RequiresUniqueEmail
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */
        // MembershipProvider Methods
        /* ---------------------------------------- */

        public override void Initialize(
          string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            if (String.IsNullOrEmpty(name))
                name = "OdbcMembershipProvider";
            if (string.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Odbc membership provider");
            }
            base.Initialize(name, config);
            _connectionStringName = config["connectionStringName"];
            if (String.IsNullOrEmpty(_connectionStringName))
            {
                throw new ProviderException("No connection string was specified.\n");
            }
            _connectionString = ConfigurationManager.ConnectionStrings[
              _connectionStringName].ConnectionString;
            _Utils = new Utils();
        }

        /* ---------------------------------------- */

        public override bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            try
            {
                // Query the database to validate user credentials
                string query = "SELECT user_password FROM `users` WHERE username = ?";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    command.Parameters.AddWithValue("username", username);
                    connection.Open();
                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        string hashedPassword = result.ToString();
                        return _Utils.PasswordVerify(password, hashedPassword);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error validating user: " + ex.Message);
                return false;
            }
        }

        /* ---------------------------------------- */

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            try
            {
                string query = "SELECT * FROM `users` WHERE username = ?";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    using (OdbcCommand command = new OdbcCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("username", username);
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string sUserName = reader["username"].ToString();
                                string sEmail = reader["user_email"].ToString();
                                string sPassword = reader["user_password"].ToString();
                                DateTime dCreationDate = _Utils.ConvertDate(reader["user_regdate"].ToString());
                                DateTime dLastLoginDate = _Utils.ConvertDate(reader["user_session_time"].ToString());

                                if (dLastLoginDate == new DateTime(1970, 1, 1))
                                {
                                    dLastLoginDate = dCreationDate;
                                }

                                DateTime dLastActivityDate = _Utils.ConvertDate(reader["user_session_time"].ToString());
                                if (dLastActivityDate == new DateTime(1970, 1, 1))
                                {
                                    dLastActivityDate = dLastLoginDate;
                                }

                                Int32 status = Convert.ToInt32(reader["user_active"]?.ToString() ?? "0");
                                bool approved = (status == 0) ? false : true;
                                bool locked = (status == 0) ? true : false;

                                return new MembershipUser(
                                    Name,              // Provider name
                                    sUserName,         // UserName
                                    null,              // ProviderUserKey
                                    sEmail,            // Email
                                    string.Empty,      // PasswordQuestion
                                    sPassword,         // Comment
                                    approved,          // IsApproved
                                    locked,            // IsLockedOut
                                    dCreationDate,     // CreationDate
                                    dLastLoginDate,    // LastLoginDate
                                    dLastActivityDate, // LastActivityDate
                                    dCreationDate,     // LastPasswordChangedDate
                                    dCreationDate      // LastLockoutDate
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUser() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while retrieving the user.");
            }

            return null;
        }


        /* ---------------------------------------- */

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            MembershipUserCollection users = new MembershipUserCollection();
            totalRecords = 0;

            try
            {
                string query = "SELECT COUNT(*) FROM `users`";
                string selectQuery = "SELECT * FROM `users` ORDER BY username OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Get total number of users
                    using (OdbcCommand countCommand = new OdbcCommand(query, connection))
                    {
                        totalRecords = Convert.ToInt32(countCommand.ExecuteScalar());
                    }

                    // Retrieve users with pagination
                    using (OdbcCommand selectCommand = new OdbcCommand(selectQuery, connection))
                    {
                        selectCommand.Parameters.AddWithValue("offset", pageIndex * pageSize);
                        selectCommand.Parameters.AddWithValue("fetchNext", pageSize);

                        using (OdbcDataReader reader = selectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string sUserName = reader["username"].ToString();
                                string sEmail = reader["user_email"].ToString();
                                string sPassword = reader["user_password"].ToString();
                                DateTime dCreationDate = _Utils.ConvertDate(reader["user_regdate"].ToString());
                                DateTime dLastLoginDate = _Utils.ConvertDate(reader["user_session_time"].ToString());

                                if (dLastLoginDate == new DateTime(1970, 1, 1))
                                {
                                    dLastLoginDate = dCreationDate;
                                }

                                DateTime dLastActivityDate = _Utils.ConvertDate(reader["user_session_time"].ToString());
                                if (dLastActivityDate == new DateTime(1970, 1, 1))
                                {
                                    dLastActivityDate = dLastLoginDate;
                                }

                                Int32 status = Convert.ToInt32(reader["user_active"]?.ToString() ?? "0");
                                bool approved = (status == 0) ? false : true;
                                bool locked = (status == 0) ? true : false;

                                MembershipUser user = new MembershipUser(
                                    Name,              // Provider name
                                    sUserName,         // UserName
                                    null,              // ProviderUserKey
                                    sEmail,            // Email
                                    string.Empty,      // PasswordQuestion
                                    sPassword,         // Comment
                                    approved,          // IsApproved
                                    locked,            // IsLockedOut
                                    dCreationDate,     // CreationDate
                                    dLastLoginDate,    // LastLoginDate
                                    dLastActivityDate, // LastActivityDate
                                    dCreationDate,     // LastPasswordChangedDate
                                    dCreationDate      // LastLockoutDate
                                );

                                users.Add(user);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllUsers() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while retrieving all users.");
            }

            return users;
        }


        /* ---------------------------------------- */

        public override int GetNumberOfUsersOnline()
        {
            System.Diagnostics.Debug.WriteLine("GetNumberOfUsersOnline()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool ChangePassword(
          string username, string oldPassword, string newPassword)
        {
            System.Diagnostics.Debug.WriteLine("ChangePassword()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool ChangePasswordQuestionAndAnswer(
          string username, string password,
          string newPasswordQuestion, string newPasswordAnswer)
        {
            System.Diagnostics.Debug.WriteLine("ChangePasswordQuestionAndAnswer()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */
        public override MembershipUser CreateUser(
            string username, string password, string email,
            string passwordQuestion, string passwordAnswer,
            bool isApproved, object providerUserKey,
            out MembershipCreateStatus status)
        {
            lock (this)
            {
                try
                {
                    // Check if the username already exists in the database
                    if (_Utils.IsExistingUsername(username, _connectionString))
                    {
                        status = MembershipCreateStatus.DuplicateUserName;
                        return null;
                    }

                    // Generate Unix timestamp for registration date
                    int registrationDate = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                    // Hash the password (you'll need to use the same hashing algorithm phpBB uses)
                    string hashedPassword = _Utils.PasswordHash(password);

                    // Construct the SQL query to insert the new user into the phpBB_users table
                    string insertUserQuery = $"INSERT INTO users (username, user_password, user_email, user_regdate, user_session_time, user_active) " +
                                             $"VALUES ('{username}', '{hashedPassword}', '{email}', {registrationDate}, {registrationDate}, {Convert.ToInt32(isApproved)})";

                    // Execute the insert query
                    _Utils.ExecuteNonQuery(insertUserQuery, _connectionString);

                    // Get the user's ID (assuming it's an auto-increment primary key)
                    int userId = _Utils.GetLastInsertedUserId(_connectionString);

                    // If you have additional logic for user approval, you can implement it here

                    // Create a MembershipUser object with the provided information
                    MembershipUser user = new MembershipUser(
                        Name,
                        username,
                        providerUserKey,
                        email,
                        passwordQuestion,
                        null, // No need to store password answer in this context
                        isApproved,
                        false, // isLockedOut
                        DateTime.UtcNow, // Creation date
                        DateTime.UtcNow, // Last login date (initially set to creation date)
                        DateTime.UtcNow, // Last activity date (initially set to creation date)
                        DateTime.UtcNow, // Last password change date (initially set to creation date)
                        DateTime.UtcNow // Last lockout date (initially set to creation date)
                    );

                    status = MembershipCreateStatus.Success;

                    return user;
                }
                catch (Exception ex)
                {
                    // If any exception occurs during user creation, set status accordingly
                    status = MembershipCreateStatus.ProviderError;
                    // Log the exception or handle it appropriately
                    System.Diagnostics.Debug.WriteLine($"[OdbcProvider]: Error creating user: {ex.Message}");
                    return null;
                }
            }
        }



        /* ---------------------------------------- */

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Username cannot be null or empty.");

            try
            {
                string deleteQuery = "DELETE FROM `users` WHERE username = ?";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    using (OdbcCommand command = new OdbcCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("username", username);
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteUser() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while deleting the user.");
            }
        }


        /* ---------------------------------------- */

        public override MembershipUserCollection FindUsersByEmail(
          string emailToMatch, int pageIndex,
          int pageSize, out int totalRecords)
        {
            System.Diagnostics.Debug.WriteLine("FindUsersByEmail()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */
        public override MembershipUserCollection FindUsersByName(
          string usernameToMatch, int pageIndex,
          int pageSize, out int totalRecords)
        {
            System.Diagnostics.Debug.WriteLine("FindUsersByName()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string GetPassword(
          string username, string answer)
        {
            System.Diagnostics.Debug.WriteLine("GetPassword()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override MembershipUser GetUser(
          object providerUserKey, bool userIsOnline)
        {
            System.Diagnostics.Debug.WriteLine("GetUser()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string GetUserNameByEmail(string email)
        {
            System.Diagnostics.Debug.WriteLine("GetUserNameByEmail()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string ResetPassword(
          string username, string answer)
        {
            System.Diagnostics.Debug.WriteLine("ResetPassword()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool UnlockUser(string userName)
        {
            System.Diagnostics.Debug.WriteLine("UnlockUser()");
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override void UpdateUser(MembershipUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            try
            {
                // Construct the SQL query to update the user information in the database
                string updateUserQuery = "UPDATE users SET user_email = ?, user_active = ? WHERE username = ?";

                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();

                    // Execute the update query
                    using (OdbcCommand command = new OdbcCommand(updateUserQuery, connection))
                    {
                        command.Parameters.AddWithValue("user_email", user.Email);
                        command.Parameters.AddWithValue("user_active", user.IsApproved ? 1 : 0);
                        command.Parameters.AddWithValue("username", user.UserName);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception appropriately
                Debug.WriteLine($"UpdateUser() Exception: {ex.Message}");
                throw new ProviderException("An error occurred while updating the user.");
            }
        }
    }
}