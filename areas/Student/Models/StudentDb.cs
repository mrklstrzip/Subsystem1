using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using AdminApi = Subsystem1.Areas.Admin.Controllers.AdminApiController;

namespace Subsystem1.Areas.Student.Models
{
    // ------------------------------------------------------------------
    // Collections and small helpers shared by every Student page.
    //
    // Collections used in Sia_set1_db:
    //   student_titles         (new)  the capstone title a project manager submitted
    //   student_activity       (new)  that student's activity log
    //   student_notifications  (new)  that student's notification drawer
    //   account_requests       (new)  "Request an Account" submissions
    //   users, rooms                  read (users: also change own password)
    //   defense_schedules             READ ONLY here. The Coordinator / Dean modules write it:
    //     { projectManagerId: "<users._id>", titleId: "<student_titles._id>" (optional),
    //       date: "yyyy-MM-dd", start: 540, end: 720,          // minutes after midnight
    //       roomId: "<rooms._id>",
    //       panelists: [ { name, role, department, photo } ],
    //       status: "approved" | "completed" | "cancelled",     // only these reach the student
    //       approvedAt: <date> }
    // ------------------------------------------------------------------
    public static class StudentDb
    {
        public static IMongoCollection<BsonDocument> Users() { return Db.Database.GetCollection<BsonDocument>("users"); }
        public static IMongoCollection<BsonDocument> Titles() { return Db.Database.GetCollection<BsonDocument>("student_titles"); }
        public static IMongoCollection<BsonDocument> Activity() { return Db.Database.GetCollection<BsonDocument>("student_activity"); }
        public static IMongoCollection<BsonDocument> Notifications() { return Db.Database.GetCollection<BsonDocument>("student_notifications"); }
        public static IMongoCollection<BsonDocument> Requests() { return Db.Database.GetCollection<BsonDocument>("account_requests"); }
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
            if (d == null || !d.TryGetValue(name, out v) || v == null || v.IsBsonNull) return "";
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

        // BSIT -> "BS Information Technology" (full names pass through unchanged)
        public static string ProgramName(string program)
        {
            switch ((program ?? "").Trim().ToUpperInvariant())
            {
                case "BSIT": return "BS Information Technology";
                case "BSCS": return "BS Computer Science";
                case "BSIS": return "BS Information System";
                default: return (program ?? "").Trim();
            }
        }

        // Faculty (adviser or professor/panelist) can be a student's adviser, the same rule the
        // Admin uses when it creates a student. Used for "Professor / Advisor" and Request Account.
        public static bool IsFaculty(BsonDocument d)
        {
            string key = AdminApi.RoleKey(Str(d, "role"));
            return key == "adviser" || key == "panelist";
        }

        public static List<BsonDocument> Advisers()
        {
            return Users().Find(new BsonDocument()).ToList()
                .Where(d => Str(d, "email").Trim() != "" && IsActive(d) && IsFaculty(d))
                .OrderBy(d => Str(d, "lastName"), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static ObjectId? ParseId(string id)
        {
            ObjectId oid;
            return ObjectId.TryParse(Clean(id), out oid) ? (ObjectId?)oid : null;
        }
    }
}
