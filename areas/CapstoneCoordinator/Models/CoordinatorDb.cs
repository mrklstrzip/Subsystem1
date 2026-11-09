using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    // ------------------------------------------------------------------
    // Collections used by the Capstone Coordinator in Sia_set1_db:
    //   defense_schedules        (shared) one document per group's defense. The Coordinator writes:
    //     { projectManagerId, titleId, title, authors: [..], manager, program: "BSIT", section, group,
    //       adviserId, adviser,
    //       date: "yyyy-MM-dd", start: 480, end: 600, roomId, roomName,      // minutes after midnight
    //       panelists: [ { userId, name, role: "Panelist", photo, match: "high|medium|low" } ],
    //       status: "generated" (To Submit) | "under-review" (conflict) | "submitted" (waiting for the Dean)
    //               | "approved" | "declined" | "completed" | "cancelled"     (the last three set by Dean / others),
    //       problem, generatedAt, submittedAt, createdAt, updatedAt }
    //   schedule_conflicts (new) { scheduleId, titleId, type, description, steps: [{title, text}],
    //                              status: "in-progress" | "resolved", detectedAt, resolvedAt }
    //   schedule_claims    (new) { titleId (unique) }  makes sure a title is scheduled only once
    //   coordinator_activity      (new) { text, at }
    //   coordinator_notifications (new) { coordinatorId, key, type, title, text, read, createdAt }
    // ------------------------------------------------------------------
    public static class CoDb
    {
        public const int PanelSize = 3;
        public const int FirstMinute = 480;     // 8:00 AM
        public const int LastMinute = 1020;     // 5:00 PM

        public static IMongoCollection<BsonDocument> Conflicts() { return Db.Database.GetCollection<BsonDocument>("schedule_conflicts"); }
        public static IMongoCollection<BsonDocument> Claims() { return Db.Database.GetCollection<BsonDocument>("schedule_claims"); }
        public static IMongoCollection<BsonDocument> Activity() { return Db.Database.GetCollection<BsonDocument>("coordinator_activity"); }
        public static IMongoCollection<BsonDocument> Notes() { return Db.Database.GetCollection<BsonDocument>("coordinator_notifications"); }

        public static string ProgramCode(string program)
        {
            string p = (program ?? "").Trim();
            switch (p)
            {
                case "BS Information Technology": return "BSIT";
                case "BS Computer Science": return "BSCS";
                case "BS Information System": return "BSIS";
                case "BS Information Systems": return "BSIS";
                default: return p.ToUpperInvariant();
            }
        }

        public static string ProgramFull(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "BSIT": return "BS Information Technology";
                case "BSCS": return "BS Computer Science";
                case "BSIS": return "BS Information System";
                default: return code ?? "";
            }
        }

        // "SBIT-4A" style label; falls back to the program code
        public static string SectionLabel(string section, string program)
        {
            return (section ?? "").Trim() != "" ? section.Trim() : (program ?? "");
        }

        // css class of the program badge
        public static string BadgeClass(string program)
        {
            switch ((program ?? "").ToUpperInvariant())
            {
                case "BSCS": return "sbcs";
                case "BSIS": return "sbis";
                default: return "sbit";
            }
        }

        public static string FmtMinutes(int m)
        {
            int h = m / 60, min = m % 60;
            return ((h % 12) == 0 ? 12 : h % 12) + ":" + min.ToString("00", CultureInfo.InvariantCulture) + (h >= 12 ? " PM" : " AM");
        }

        // "8:30AM" (the style used in the Defense Schedule table)
        public static string FmtCompact(int m) { return FmtMinutes(m).Replace(" ", ""); }

        public static string Hhmm(int m)
        {
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static int? ParseHhmm(string s)
        {
            DateTime t;
            if (DateTime.TryParseExact((s ?? "").Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
            {
                return t.Hour * 60 + t.Minute;
            }
            return null;
        }

        public static DateTime? ParseDay(string s)
        {
            DateTime d;
            if (DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d.Date;
            return null;
        }

        public static string DayText(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        public static string StatusOf(BsonDocument d) { return Str(d, "status").Trim().ToLowerInvariant(); }

        // Coordinator-side status: to-submit | under-review | submitted, or null when it is not on the list
        public static string CoStatus(BsonDocument d)
        {
            switch (StatusOf(d))
            {
                case "generated":
                case "declined":
                case "rejected":
                    return "to-submit";
                case "under-review":
                    return "under-review";
                case "submitted":
                case "pending":
                case "approved":
                case "completed":
                case "complete":
                    return "submitted";
                default:
                    return null;
            }
        }

        // the schedule keeps a room / people busy (same rule the Campus Director uses, plus cancelled)
        public static bool HoldsRoom(BsonDocument d)
        {
            string s = StatusOf(d);
            return s != "draft" && s != "pending" && s != "rejected" && s != "declined" && s != "cancelled" && s != "canceled";
        }

        public static bool IsEditable(BsonDocument d)
        {
            string s = CoStatus(d);
            return s == "to-submit" || s == "under-review";
        }

        public static void Log(string text)
        {
            try
            {
                Activity().InsertOne(new BsonDocument { { "text", text }, { "at", DateTime.UtcNow } });
            }
            catch
            {
                // the log is a convenience; never block the real work
            }
        }

        public static List<string> Keywords(BsonDocument title)
        {
            var list = new List<string>();
            BsonValue kv;
            if (title != null && title.TryGetValue("keywords", out kv) && kv != null && kv.IsBsonArray)
            {
                foreach (BsonValue k in kv.AsBsonArray) if (k.IsString) list.Add(k.AsString);
            }
            return list;
        }

        public static List<string> Expertise(BsonDocument user)
        {
            var list = new List<string>();
            BsonValue ev;
            if (user.TryGetValue("expertise", out ev) && ev != null && ev.IsBsonArray)
            {
                foreach (BsonValue k in ev.AsBsonArray) if (k.IsString && k.AsString.Trim() != "") list.Add(k.AsString.Trim());
            }
            return list;
        }

        public static bool IsFree(BsonDocument roomOrBuilding)
        {
            return !string.Equals(Str(roomOrBuilding, "status"), "Unavailable", StringComparison.OrdinalIgnoreCase) && IsActive(roomOrBuilding);
        }
    }
}
