using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.Student.Models
{
    public class NotificationItem
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }   // Philippine time
        public bool Read { get; set; }

        public string Icon
        {
            get
            {
                if (Type == "defense-today") return "fa-calendar-day";
                if (Type == "defense-tomorrow") return "fa-clock";
                return "fa-bell";
            }
        }
    }

    /// <summary>
    /// A student's notification drawer, saved in student_notifications:
    ///   { studentId, key, type, title, text, read, createdAt }
    /// The defense reminders (the day before, and the day of) are created here when they become due.
    /// Other modules may add their own documents with the same fields.
    /// </summary>
    public static class StudentNotifications
    {
        private static bool indexed;

        private static void EnsureIndex()
        {
            if (indexed) return;
            try
            {
                // One reminder per student per key, even if two pages load at the same moment.
                Notifications().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Combine(
                        Builders<BsonDocument>.IndexKeys.Ascending("studentId"),
                        Builders<BsonDocument>.IndexKeys.Ascending("key")),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = new BsonDocument("key", new BsonDocument("$exists", true))
                    }));
                indexed = true;
            }
            catch
            {
                // Notifications still work without the index; try it again next time.
            }
        }

        private static void Remind(string studentId, string key, string type, string title, string text)
        {
            var f = Builders<BsonDocument>.Filter;
            var u = Builders<BsonDocument>.Update;
            try
            {
                // Inserts the reminder the first time only; it never overwrites a read flag.
                Notifications().UpdateOne(
                    f.And(f.Eq("studentId", studentId), f.Eq("key", key)),
                    u.Combine(
                        u.SetOnInsert("type", type),
                        u.SetOnInsert("title", title),
                        u.SetOnInsert("text", text),
                        u.SetOnInsert("read", false),
                        u.SetOnInsert("createdAt", DateTime.UtcNow)),
                    new UpdateOptions { IsUpsert = true });
            }
            catch (MongoWriteException)
            {
                // Lost a race with another request that created the same reminder. Nothing to do.
            }
        }

        /// <summary>Creates any reminder that is due, then returns the newest notifications.</summary>
        public static List<NotificationItem> Load(BsonDocument me, SubmissionStore s)
        {
            string sid = DocId(me);
            DateTime now = NowManila();

            EnsureIndex();

            if (s.DefenseScheduled && !s.DefenseComplete && s.DefenseDate.HasValue)
            {
                DateTime d = s.DefenseDate.Value;
                string time = d.ToString("h:mm tt", CultureInfo.InvariantCulture);
                string where = s.Room != null ? " in " + s.Room : "";
                string day = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (d.Date == now.Date)
                {
                    Remind(sid, "defense-today:" + day, "defense-today", "Defense Schedule Today!",
                        "Your Defense is scheduled today at " + time + where + ".");
                }
                else if (d.Date == now.Date.AddDays(1))
                {
                    Remind(sid, "defense-tomorrow:" + day, "defense-tomorrow", "Defense Schedule Tomorrow",
                        "Reminder: Your Capstone Defense is scheduled for tomorrow at " + time + where + ".");
                }
            }

            return Notifications()
                .Find(Builders<BsonDocument>.Filter.Eq("studentId", sid))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(30)
                .ToList()
                .Select(n =>
                {
                    BsonValue rv;
                    bool read = n.TryGetValue("read", out rv) && rv != null && rv.IsBoolean && rv.AsBoolean;
                    return new NotificationItem
                    {
                        Id = DocId(n),
                        Type = Str(n, "type"),
                        Title = Str(n, "title"),
                        Text = Str(n, "text"),
                        CreatedAt = Stamp(n, "createdAt") ?? now,
                        Read = read
                    };
                })
                .ToList();
        }

        public static void MarkRead(string studentId, string id)
        {
            ObjectId? oid = ParseId(id);
            if (!oid.HasValue) return;

            var f = Builders<BsonDocument>.Filter;
            Notifications().UpdateOne(
                f.And(f.Eq("_id", oid.Value), f.Eq("studentId", studentId)),
                Builders<BsonDocument>.Update.Set("read", true));
        }
    }
}
