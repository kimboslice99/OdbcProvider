/* ======================================== */
//
// Thanks to Robert McMurray for a good example to start from :)
//
/* ======================================== */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Data.Odbc;
using System.Linq;
using System.Web.Security;

namespace OdbcProvider
{
    public class OdbcMembershipProvider : MembershipProvider
    {
        private Dictionary<string, MembershipUser> _Users;
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
            if (String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
                return false;

            try
            {
                // Retrieve user data from the membership data store
                ReadMembershipDataStore();

                MembershipUser user;
                if (_Users.TryGetValue(username, out user))
                {
                    if (_Utils.PasswordVerify(password, user.Comment))
                    {
                        // Check if user is not locked out and is approved
                        if (!user.IsLockedOut && user.IsApproved)
                        {
                            return true; // User is authenticated
                        }
                    }
                }
                return false; // User is not authenticated
            }
            catch (Exception ex)
            {
                // Log or handle the exception appropriately
                System.Diagnostics.Debug.WriteLine("Error validating user: " + ex.Message);
                return false;
            }
        }

        /* ---------------------------------------- */

        public override MembershipUser GetUser(
          string username, bool userIsOnline)
        {
            if (String.IsNullOrEmpty(username)) return null;
            ReadMembershipDataStore();
            try
            {
                MembershipUser user;
                if (_Users.TryGetValue(username, out user)) return user;
            }
            catch (Exception ex)
            {
                throw new ProviderException("Error: " + ex.Message);
            }
            return null;
        }

        /* ---------------------------------------- */

        public override MembershipUserCollection GetAllUsers(
          int pageIndex, int pageSize, out int totalRecords)
        {
            ReadMembershipDataStore();
            MembershipUserCollection users = new MembershipUserCollection();
            if ((pageIndex >= 0) && (pageSize >= 1))
            {
                try
                {
                    foreach (KeyValuePair<string, MembershipUser> pair
                      in _Users.Skip(pageIndex * pageSize).Take(pageSize))
                    {
                        users.Add(pair.Value);
                    }
                }
                catch (Exception ex)
                {
                    throw new ProviderException("Error: " + ex.Message);
                }
            }
            totalRecords = _Users.Count;
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
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool ChangePasswordQuestionAndAnswer(
          string username, string password,
          string newPasswordQuestion, string newPasswordAnswer)
        {
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
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override MembershipUserCollection FindUsersByEmail(
          string emailToMatch, int pageIndex,
          int pageSize, out int totalRecords)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */
        public override MembershipUserCollection FindUsersByName(
          string usernameToMatch, int pageIndex,
          int pageSize, out int totalRecords)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string GetPassword(
          string username, string answer)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override string ResetPassword(
          string username, string answer)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override bool UnlockUser(string userName)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */

        public override void UpdateUser(MembershipUser user)
        {
            throw new NotSupportedException();
        }

        /* ---------------------------------------- */
        // MembershipProvider helper method
        /* ---------------------------------------- */

        public void ReadMembershipDataStore()
        {
            lock (this)
            {
                if (_Users == null)
                {
                    try
                    {
                        _Users = new Dictionary<string, MembershipUser>(16, StringComparer.InvariantCultureIgnoreCase);
                        string queryString = "SELECT * FROM users WHERE user_id > 0";

                        using (OdbcConnection connection = new OdbcConnection(_connectionString))
                        {
                            OdbcCommand command = new OdbcCommand(queryString, connection);
                            connection.Open();

                            OdbcDataReader reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                string sUserName = reader["username"].ToString();
                                string sEmail = reader["user_email"].ToString();
                                string sPassword = reader["user_password"].ToString();
                                DateTime dCreationDate =  _Utils.ConvertDate(reader["user_regdate"].ToString());
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
                                    String.Empty,      // PasswordQuestion
                                    sPassword,         // Comment
                                    approved,          // IsApproved
                                    locked,            // IsLockedOut
                                    dCreationDate,     // CreationDate
                                    dLastLoginDate,    // LastLoginDate
                                    dLastActivityDate, // LastActivityDate
                                    dCreationDate,     // LastPasswordChangedDate
                                    dCreationDate      // LastLockoutDate
                                    );
                                _Users.Add(user.UserName, user);
                            }
                            reader.Close();
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
}