using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MongoDB.Bson;

namespace Subsystem1.Data
{
    // Schedules written by the Coordinator may list a panelist by name only, so faculty
    // are matched by a normalised name as well as by their account id.
    public static class FacultyNames
    {
        private static string Str(BsonDocument d, string name)
        {
            BsonValue v;
            if (d.TryGetValue(name, out v) && v != null && v.IsString) return v.AsString;
            return "";
        }

        // "Dr. Naruto  Uzumaki" -> "naruto uzumaki"  (titles, dots and commas are ignored)
        public static string Key(string name)
        {
            string s = (name ?? "").ToLowerInvariant();
            s = Regex.Replace(s, @"\b(dr|prof|professor|engr|engineer|mr|mrs|ms|atty|phd)\b\.?", " ");
            s = Regex.Replace(s, @"[^a-z0-9\u00f1 ]", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        // Every spelling of this user's name that we accept.
        public static HashSet<string> Variants(BsonDocument user)
        {
            string first = Str(user, "firstName").Trim();
            string last = Str(user, "lastName").Trim();
            string middle = Str(user, "middleName").Trim();
            if (middle == "") middle = Str(user, "middlename").Trim();

            var set = new HashSet<string>(new[]
            {
                Key(first + " " + last),
                Key(first + " " + middle + " " + last),
                Key(last + " " + first),
                Key(last + " " + first + " " + middle)
            }.Where(v => v != ""));
            return set;
        }
    }
}
