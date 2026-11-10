using System;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Subsystem1.Data
{
    // Request body of /<Area>/<Area>Api/updateProfile (the Edit Profile form on every Profile page).
    public class ProfileDto
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }

    // ------------------------------------------------------------------
    // One place that checks and saves "Edit Profile" for every role (name and e-mail).
    // The profile picture is gone: the UI shows the user's initials instead.
    // ------------------------------------------------------------------
    public static class ProfileUpdater
    {
        private static readonly Regex EmailPattern =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static readonly Regex NamePattern =
            new Regex(@"^\p{L}[\p{L}\s.'\-]*$", RegexOptions.Compiled);

        private static string Clean(string s)
        {
            return Regex.Replace((s ?? "").Trim(), @"\s+", " ");
        }

        /// <summary>
        /// Validates and saves. Returns an error message, or null when saved
        /// (the cleaned values come back through the out parameters).
        /// </summary>
        public static string Update(BsonDocument me, ProfileDto dto,
            out string firstName, out string middleName, out string lastName, out string email)
        {
            firstName = middleName = lastName = email = "";

            if (me == null) return "Your session has expired. Please log in again.";
            if (dto == null) return "No data received.";

            firstName = Clean(dto.FirstName);
            middleName = Clean(dto.MiddleName);
            lastName = Clean(dto.LastName);
            email = Clean(dto.Email).ToLowerInvariant();

            if (firstName == "" || lastName == "" || email == "")
                return "Please fill in your first name, last name and email.";

            if (firstName.Length > 50 || middleName.Length > 50 || lastName.Length > 50)
                return "Names can be at most 50 characters.";

            if (!NamePattern.IsMatch(firstName) || !NamePattern.IsMatch(lastName)
                || (middleName != "" && !NamePattern.IsMatch(middleName)))
                return "Names can only contain letters, spaces, periods, hyphens and apostrophes.";

            if (email.Length > 254 || !EmailPattern.IsMatch(email))
                return "Please enter a valid email address.";

            var users = Db.Database.GetCollection<BsonDocument>("users");

            // the e-mail is the login, so it must stay unique
            var sameEmail = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Regex("email",
                    new BsonRegularExpression("^" + Regex.Escape(email) + "$", "i")),
                Builders<BsonDocument>.Filter.Ne("_id", me["_id"]));
            if (users.Find(sameEmail).Limit(1).FirstOrDefault() != null)
                return "That email is already used by another account.";

            var u = Builders<BsonDocument>.Update;
            users.UpdateOne(
                Builders<BsonDocument>.Filter.Eq("_id", me["_id"]),
                u.Combine(
                    u.Set("firstName", firstName),
                    u.Set("middleName", middleName),
                    u.Set("lastName", lastName),
                    u.Set("email", email),
                    u.Set("updatedAt", DateTime.UtcNow)));

            return null;
        }
    }
}
