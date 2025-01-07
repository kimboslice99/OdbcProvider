using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Web.Security;
using System.Xml.Linq;

namespace OdbcProvider
{
    public class OdbcMembershipProvider : MembershipProvider
    {
        private string _connectionStringName;
        private string _connectionString;
        private Utils _Utils;
        private readonly object _createUserLock = new object();

        /* ---------------------------------------- */
        // MembershipProvider Properties
        /* ---------------------------------------- */

        public override string ApplicationName
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override string Name
        {
            get { return "OdbcMembershipProvider"; }
        }

        /* ---------------------------------------- */

        public override bool EnablePasswordRetrieval
        {
            get { return false; }
        }

        /* ---------------------------------------- */

        public override bool EnablePasswordReset
        {
            get { return true; }
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
            get { return MembershipPasswordFormat.Hashed; }
        }

        /* ---------------------------------------- */

        public override string PasswordStrengthRegularExpression
        {
            get { throw new NotSupportedException(); }
        }

        /* ---------------------------------------- */

        public override bool RequiresQuestionAndAnswer
        {
            get { return false; }
        }

        /* ---------------------------------------- */

        public override bool RequiresUniqueEmail
        {
            get { return true; }
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
                throw new ProviderException("No connection string was specified.");
            }

            _connectionString = ConfigurationManager.ConnectionStrings[_connectionStringName].ConnectionString;
            _Utils = new Utils();
            _Utils.DatabaseInit(_connectionString);
        }

        public void ComInit(NameValueCollection config)
        {
            _connectionString = config["connectionString"];
            if (String.IsNullOrEmpty(_connectionString))
            {
                throw new ProviderException("No connection string was specified.");
            }
            _Utils = new Utils();
            _Utils.DatabaseInit(_connectionString);
            base.Initialize("OdbcMembershipProvider", config);
        }

        /* ---------------------------------------- */

        public override bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            try
            {
                // Query the database to validate user credentials
                string query = "SELECT user_password FROM users WHERE user_name = ?";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("user_name", username);
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
                _Utils.WriteDebug($"ValidateUser() {ex.Message}");
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
                string query = "SELECT * FROM users WHERE user_name = ?";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("user_name", username);
                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string sUserName = Convert.ToString(reader["user_name"]);
                            string sEmail = Convert.ToString(reader["user_email"]);
                            string sPassword = Convert.ToString(reader["user_password"]);
                            DateTime CreationDate = Convert.ToDateTime(reader["user_regdate"]);
                            DateTime lastLoginDate = Convert.ToDateTime(reader["user_last_login"]);
                            DateTime lastActivityDate = Convert.ToDateTime(reader["user_last_activity"]);
                            DateTime lastPasswordChangedDate = Convert.ToDateTime(reader["user_last_password_changed"]);
                            DateTime lastLockoutDate = Convert.ToDateTime(reader["user_last_lockout"]);
                            string passwordQuestion = Convert.ToString(reader["user_password_question"]);
                            string passwordAnswer = Convert.ToString(reader["user_password_answer"]);

                            bool userLocked = Convert.ToBoolean(reader["user_locked"]);
                            bool userActive = Convert.ToBoolean(reader["user_approved"]);

                            MembershipUser user = new MembershipUser(
                                Name,              // Provider name
                                sUserName,         // UserName
                                null,              // ProviderUserKey
                                sEmail,            // Email
                                passwordQuestion,      // PasswordQuestion
                                passwordAnswer,         // Comment
                                userActive,          // IsApproved
                                userLocked,            // IsLockedOut
                                CreationDate,     // CreationDate
                                lastLoginDate,    // LastLoginDate
                                lastActivityDate, // LastActivityDate
                                lastPasswordChangedDate,     // LastPasswordChangedDate
                                lastLockoutDate      // LastLockoutDate
                            );

                            if (userIsOnline)
                            {
                                user.LastActivityDate = DateTime.Now;
                                UpdateUser(user);
                            }

                            return user;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetUser() {ex.Message}");
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
                string query = "SELECT COUNT(*) FROM users";
                string selectQuery = "SELECT * FROM users ORDER BY user_name OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

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
                                string sUserName = Convert.ToString(reader["user_name"]);
                                string sEmail = Convert.ToString(reader["user_email"]);
                                string sPassword = Convert.ToString(reader["user_password"]);
                                DateTime dCreationDate = Convert.ToDateTime(reader["user_regdate"]);
                                DateTime lastLoginDate = Convert.ToDateTime(reader["user_last_login"]);
                                DateTime lastActivityDate = Convert.ToDateTime(reader["user_last_activity"]);
                                DateTime lastPasswordChangedDate = Convert.ToDateTime(reader["user_last_password_changed"]);
                                DateTime lastLockoutDate = Convert.ToDateTime(reader["user_last_lockout"]);
                                string passwordQuestion = Convert.ToString(reader["user_password_question"]);
                                string passwordAnswer = Convert.ToString(reader["user_password_answer"]);

                                bool locked = Convert.ToBoolean(reader["user_locked"]);
                                bool approved = Convert.ToBoolean(reader["user_approved"]);

                                MembershipUser user = new MembershipUser(
                                    Name,              // Provider name
                                    sUserName,         // UserName
                                    null,              // ProviderUserKey
                                    sEmail,            // Email
                                    passwordQuestion,      // PasswordQuestion
                                    passwordAnswer,         // Comment
                                    approved,          // IsApproved
                                    locked,            // IsLockedOut
                                    dCreationDate,     // CreationDate
                                    lastLoginDate,    // LastLoginDate
                                    lastActivityDate, // LastActivityDate
                                    lastPasswordChangedDate,     // LastPasswordChangedDate
                                    lastLockoutDate      // LastLockoutDate
                                );

                                users.Add(user);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"GetAllUsers() {ex.Message}");
                throw new ProviderException("An error occurred while retrieving all users.");
            }

            return users;
        }


        /* ---------------------------------------- */

        public override int GetNumberOfUsersOnline()
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool ChangePassword(
          string username, string oldPassword, string newPassword)
        {
            _Utils.WriteDebug("ChangePassword()");
            if (_Utils.IsExistingUsername(username, _connectionString))
            {
                if(ValidateUser(username, oldPassword))
                {
                    string hash = _Utils.PasswordHash(newPassword);
                    Dictionary<string, object> values = new Dictionary<string, object>
                    {
                        { "user_password", hash },
                        { "user_name", username }
                    };
                    string query = "UPDATE `users` SET `user_password` = ? WHERE `user_name` = ?";
                    _Utils.ExecutePreparedNonQuery(query, _connectionString, values);
                    return true;
                }
            }
            return false;
        }

        /* ---------------------------------------- */

        public override bool ChangePasswordQuestionAndAnswer(
          string username, string password,
          string newPasswordQuestion, string newPasswordAnswer)
        {
            _Utils.WriteDebug("ChangePasswordQuestionAndAnswer()");
            if(!ValidateUser(username, password))
            {
                return false;
            }
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "user_password_question", newPasswordQuestion },
                { "user_password_answer", newPasswordAnswer },
                { "user_name", username }
            };
            string query = "UPDATE users SET user_password_question = ?, user_password_answer, ? WHERE user_name = ?";
            int result = _Utils.ExecutePreparedNonQuery(query, _connectionString, parameters);
            if(result > 0)
            {
                return true;
            }
            return false;
        }

        /* ---------------------------------------- */
        public override MembershipUser CreateUser(
            string username, string password, string email,
            string passwordQuestion, string passwordAnswer,
            bool isApproved, object providerUserKey,
            out MembershipCreateStatus status)
        {
            lock (_createUserLock)
            {
                try
                {
                    // Check if the username already exists in the database
                    if (_Utils.IsExistingUsername(username, _connectionString))
                    {
                        status = MembershipCreateStatus.DuplicateUserName;
                        return null;
                    }

                    DateTime registrationDate = DateTime.UtcNow;
                    DateTime minValue = DateTime.MinValue;
                    string hashedPassword = _Utils.PasswordHash(password);

                    string insertUserQuery = "INSERT INTO users (" +
                        "user_name," +
                        "user_password," +
                        "user_email," +
                        "user_regdate," +
                        "user_last_login," +
                        "user_last_activity," +
                        "user_last_password_changed," +
                        "user_last_lockout," +
                        "user_password_question," +
                        "user_password_answer," +
                        "user_approved," +
                        "user_locked) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

                    Dictionary<string, object> values = new Dictionary<string, object>
                    {
                        { "user_name", username },
                        { "user_password", hashedPassword },
                        { "user_email", email },
                        { "user_regdate", registrationDate },
                        { "user_last_login", minValue },
                        { "user_last_activity", minValue },
                        { "user_last_password_changed", minValue },
                        { "user_last_lockout", minValue },
                        { "user_password_question", passwordQuestion },
                        { "user_password_answer", passwordAnswer },
                        { "user_approved", Convert.ToInt32(isApproved) },
                        { "user_locked", 0 },
                    };
                    // Execute the insert query
                    int result = _Utils.ExecutePreparedNonQuery(insertUserQuery, _connectionString, values);
                    // check it
                    if(result == 0)
                    {
                        throw new MembershipCreateUserException("Failed to create user");
                    }

                    int userId = _Utils.GetLastInsertedUserId(_connectionString);

                    // If you have additional logic for user approval, you can implement it here

                    // Create a MembershipUser object with the provided information
                    MembershipUser user = new MembershipUser(
                        Name,
                        username,
                        providerUserKey,
                        email,
                        passwordQuestion,
                        passwordAnswer,
                        isApproved,
                        false,
                        registrationDate,
                        minValue,
                        minValue,
                        minValue,
                        minValue
                    );

                    status = MembershipCreateStatus.Success;
                    return user;
                }
                catch (Exception ex)
                {
                    status = MembershipCreateStatus.ProviderError;
                    _Utils.WriteDebug($"Error creating user: {ex.Message}");
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
                string deleteQuery = "DELETE FROM users WHERE user_name = ?";
                using (OdbcConnection connection = new OdbcConnection(_connectionString))
                {
                    connection.Open();
                    using (OdbcCommand command = new OdbcCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("user_name", username);
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"DeleteUser() {ex.Message}");
                throw new ProviderException("An error occurred while deleting the user.");
            }
        }


        /* ---------------------------------------- */

        public override MembershipUserCollection FindUsersByEmail(
          string emailToMatch, int pageIndex,
          int pageSize, out int totalRecords)
        {
            _Utils.WriteDebug("FindUsersByEmail()");
            MembershipUserCollection collection = new MembershipUserCollection();
            totalRecords = 0;
            int offset = pageIndex * pageSize;
            string query = $"SELECT * FROM users WHERE user_name LIKE ? OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

            using (OdbcConnection conn = new OdbcConnection(_connectionString))
            using (OdbcCommand command = new OdbcCommand(query, conn))
            {
                conn.Open();
                command.Parameters.AddWithValue("user_name", emailToMatch);
                command.Parameters.AddWithValue("offset", pageIndex * pageSize);
                command.Parameters.AddWithValue("fetchNext", pageSize);

                using (OdbcDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        MembershipUser user = new MembershipUser(
                            Name,
                            Convert.ToString(reader["user_name"]),
                            null,
                            Convert.ToString(reader["user_email"]),
                            Convert.ToString(reader["user_password_question"]),
                            Convert.ToString(reader["user_password_answer"]),
                            Convert.ToBoolean(reader["user_approved"]),
                            Convert.ToBoolean(reader["user_locked"]),
                            Convert.ToDateTime(reader["user_regdate"]),
                            Convert.ToDateTime(reader["user_last_login"]),
                            Convert.ToDateTime(reader["user_last_activity"]),
                            Convert.ToDateTime(reader["user_last_password_changed"]),
                            Convert.ToDateTime(reader["user_last_lockout"])
                        );
                        collection.Add(user);
                        totalRecords++;
                    }
                }
            }

            return collection;
        }

        /* ---------------------------------------- */
        public override MembershipUserCollection FindUsersByName(
            string usernameToMatch, int pageIndex,
            int pageSize, out int totalRecords)
        {
            _Utils.WriteDebug("FindUsersByName()");
            MembershipUserCollection collection = new MembershipUserCollection();
            totalRecords = 0;
            int offset = pageIndex * pageSize;
            string query = $"SELECT * FROM users WHERE user_name LIKE ? OFFSET ? ROWS FETCH NEXT ? ROWS ONLY";

            using (OdbcConnection conn = new OdbcConnection(_connectionString))
            using (OdbcCommand command = new OdbcCommand(query, conn))
            {
                conn.Open();
                command.Parameters.AddWithValue("user_name", usernameToMatch);
                command.Parameters.AddWithValue("offset", pageIndex * pageSize);
                command.Parameters.AddWithValue("fetchNext", pageSize);

                using (OdbcDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        MembershipUser user = new MembershipUser(
                            Name,
                            Convert.ToString(reader["user_name"]),
                            null,
                            Convert.ToString(reader["user_email"]),
                            Convert.ToString(reader["user_password_question"]),
                            Convert.ToString(reader["user_password_answer"]),
                            Convert.ToBoolean(reader["user_approved"]),
                            Convert.ToBoolean(reader["user_locked"]),
                            Convert.ToDateTime(reader["user_regdate"]),
                            Convert.ToDateTime(reader["user_last_login"]),
                            Convert.ToDateTime(reader["user_last_activity"]),
                            Convert.ToDateTime(reader["user_last_password_changed"]),
                            Convert.ToDateTime(reader["user_last_lockout"])
                        );
                        collection.Add(user);
                        totalRecords++;
                    }
                }
            }

            return collection;
        }

        /* ---------------------------------------- */

        public override string GetPassword(
          string username, string answer)
        {
            throw new NotSupportedException("This provider uses BCrypt, passwords cannot be retrieved.");
        }

        /* ---------------------------------------- */

        public override MembershipUser GetUser(
          object providerUserKey, bool userIsOnline)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string GetUserNameByEmail(string email)
        {
            _Utils.WriteDebug($"GetUserNameByEmail() called for email {email}");
            string query = "SELECT user_name FROM users WHERE user_email = ?";
            using (OdbcConnection conn = new OdbcConnection(_connectionString))
            using (OdbcCommand cmd = new OdbcCommand(query, conn))
            {
                conn.Open();
                object result = cmd.ExecuteScalar();
                if(result != null)
                {
                    return result.ToString();
                }
            }
            return null;
        }

        /* ---------------------------------------- */

        public override string ResetPassword(
          string username, string answer)
        {
            _Utils.WriteDebug($"ResetPassword() called for user {username}");
            string query = "SELECT user_password_answer FROM users WHERE user_name = ?";
            using(OdbcConnection conn = new OdbcConnection(_connectionString))
            using (OdbcCommand command = new OdbcCommand(query, conn))
            {
                conn.Open();
                using(OdbcDataReader reader = command.ExecuteReader())
                {
                    if(reader.Read())
                    {
                        string userPasswordAnswer = Convert.ToString(reader["user_password_answer"]);
                        if(0 == String.Compare(answer, userPasswordAnswer, true))
                        {
                            string newPassword = Membership.GeneratePassword(12, 3);
                            query = "UPDATE users SET user_password = ? WHERE user_name = ?";
                            Dictionary<string, object> parameters = new Dictionary<string, object>
                            {
                                { "user_password", _Utils.PasswordHash(newPassword) },
                                { "user_name", username },
                            };
                            int result = _Utils.ExecutePreparedNonQuery(query, _connectionString, parameters);
                            if (result > 0)
                            {
                                return newPassword;
                            }
                        }
                    }
                }
            }
            throw new ProviderException("Unable to reset password.");
        }

        /* ---------------------------------------- */

        public override bool UnlockUser(string userName)
        {
            _Utils.WriteDebug("UnlockUser()");
            string query = "UPDATE users SET user_locked = 0 WHERE user_name = ?";
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "user_name", userName },
            };
            int result = _Utils.ExecutePreparedNonQuery(query, _connectionString, parameters);
            if (result > 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// May be called when updating user roles, making it difficult to check if values were updated successfully
        /// since effected rows will be 0 in this case
        /// </summary>
        /// <param name="user"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ProviderException"></exception>
        public override void UpdateUser(MembershipUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            try
            {
                string updateUserQuery = "UPDATE users SET user_email = ?, " +
                    "user_last_login = ?, " +
                    "user_last_activity = ?, " +
                    "user_last_password_changed = ?, " +
                    "user_last_lockout = ?, " +
                    "user_password_question = ?, " +
                    "user_password_answer = ?, " +
                    "user_approved = ?, " +
                    "user_locked = ? WHERE user_name = ?";

                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "user_email", user.Email },
                    { "user_last_login", user.LastLoginDate },
                    { "user_last_activity", user.LastActivityDate },
                    { "user_last_password_changed", user.LastPasswordChangedDate },
                    { "user_last_lockout", user.LastLockoutDate },
                    { "user_password_question", user.PasswordQuestion },
                    { "user_password_answer", user.Comment },
                    { "user_approved", Convert.ToInt16(user.IsApproved) },
                    { "user_locked", user.IsLockedOut },
                    { "user_name", user.UserName },
                };
                int result = _Utils.ExecutePreparedNonQuery(updateUserQuery, _connectionString, parameters);
                return;
            }
            catch (Exception ex)
            {
                _Utils.WriteDebug($"UpdateUser() {ex.Message}");
                throw new ProviderException($"An error occurred while updating the user. {ex.Message}", ex.InnerException);
            }
        }
    }
}