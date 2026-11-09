using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Subsystem1.Data
{
    // ------------------------------------------------------------------
    // Read-only helpers for data that other systems write into Sia_set1_db.
    //
    //   users              panelists and advisers (role, isActive, expertise, names)
    //   student_titles     the manuscripts (title, abstract, keywords, authors, adviser, studentId...)
    //   class_schedules    each faculty member's weekly class timetable
    //   defense_schedules  written by the Coordinator / Dean modules
    //   rooms              from the Campus Director module
    //
    // This system has no Student, Adviser or Panelist screens any more; it only reads their data.
    // ------------------------------------------------------------------
    public static class SharedDb
    {
        public static IMongoCollection<BsonDocument> Users() { return Db.Database.GetCollection<BsonDocument>("users"); }
        // The manuscripts. Read only.
        public static IMongoCollection<BsonDocument> Titles() { return Db.Database.GetCollection<BsonDocument>("student_titles"); }
        public static IMongoCollection<BsonDocument> Schedules() { return Db.Database.GetCollection<BsonDocument>("defense_schedules"); }
        public static IMongoCollection<BsonDocument> Rooms() { return Db.Database.GetCollection<BsonDocument>("rooms"); }

        // The Philippines has no daylight saving time, so UTC+8 is always right,
        // whatever time zone the web server itself is set to.
        public static DateTime NowManila()
        {
            return DateTime.UtcNow.AddHours(8);
        }

        public static string Str(BsonDocument d, string name)
        {
            BsonValue v;
            if (d.TryGetValue(name, out v) && v != null && v.IsString) return v.AsString;
            return "";
        }

        // Text for a field that may be stored as text, a number or an ObjectId.
        public static string AnyStr(BsonDocument d, string name)
        {
            BsonValue v;
            if (!d.TryGetValue(name, out v) || v == null || v.IsBsonNull) return "";
            if (v.IsString) return v.AsString;
            if (v.IsObjectId) return v.AsObjectId.ToString();
            if (v.IsInt32) return v.AsInt32.ToString(CultureInfo.InvariantCulture);
            if (v.IsInt64) return v.AsInt64.ToString(CultureInfo.InvariantCulture);
            if (v.IsDouble) return v.AsDouble.ToString("0.##", CultureInfo.InvariantCulture);
            return v.ToString();
        }

        // Minutes after midnight, from a number or from "09:00" / "9:00 AM" text.
        public static int? Minutes(BsonDocument d, string name)
        {
            BsonValue v;
            if (!d.TryGetValue(name, out v) || v == null) return null;
            if (v.IsInt32) return v.AsInt32;
            if (v.IsInt64) return (int)v.AsInt64;
            if (v.IsDouble) return (int)v.AsDouble;
            if (v.IsString)
            {
                DateTime t;
                string[] formats = { "H:mm", "HH:mm", "h:mm tt", "hh:mm tt" };
                if (DateTime.TryParseExact(v.AsString.Trim(), formats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                {
                    return t.Hour * 60 + t.Minute;
                }
            }
            return null;
        }

        // A stored date/time as Philippine clock time (null when the field is missing).
        public static DateTime? Stamp(BsonDocument d, string name)
        {
            BsonValue v;
            if (d.TryGetValue(name, out v) && v != null && v.IsBsonDateTime)
            {
                return v.ToUniversalTime().AddHours(8);
            }
            return null;
        }

        public static bool IsActive(BsonDocument d)
        {
            BsonValue v;
            if (d.TryGetValue("isActive", out v) && v != null && v.IsBoolean) return v.AsBoolean;
            return true;
        }

        public static string Clean(string s) { return (s ?? "").Trim(); }

        public static string DocId(BsonDocument d) { return d["_id"].ToString(); }

        public static string IdOf(BsonDocument d)
        {
            string[] names = { "studentId", "idNumber", "studentID", "employeeId" };
            foreach (string n in names)
            {
                string s = Str(d, n).Trim();
                if (s != "") return s;
            }
            return "";
        }

        public static string PlainName(BsonDocument d)
        {
            string suf = Str(d, "suffix");
            return (Str(d, "firstName") + " " + Str(d, "lastName") + (suf != "" ? " " + suf : "")).Trim();
        }

        public static ObjectId? ParseId(string id)
        {
            ObjectId oid;
            return ObjectId.TryParse(Clean(id), out oid) ? (ObjectId?)oid : null;
        }
    }
}
