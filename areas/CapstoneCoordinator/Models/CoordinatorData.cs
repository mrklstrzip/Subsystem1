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
    public class SchedRow
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Adviser { get; set; }
        public string Program { get; set; }
        public string Section { get; set; }
        public DateTime? Date { get; set; }
        public string Status { get; set; }
        public bool Declined { get; set; }

        public string Badge { get { return CoDb.SectionLabel(Section, Program); } }
        public string BadgeClass { get { return CoDb.BadgeClass(Program); } }
        public string StatusLabel
        {
            get
            {
                return Status == "under-review" ? "Under Review"
                    : Status == "submitted" ? "Submitted"
                    : "To Submit";
            }
        }
        public string DateText
        {
            get
            {
                return Date.HasValue
                    ? Date.Value.ToString("MM-dd-yy", CultureInfo.InvariantCulture)
                    : "-------";
            }
        }
    }

    public class DefenseRow
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Adviser { get; set; }
        public string Program { get; set; }
        public string Section { get; set; }
        public DateTime Date { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public string Room { get; set; }
        public string Status { get; set; }

        public string Badge { get { return CoDb.SectionLabel(Section, Program); } }
        public string BadgeClass { get { return CoDb.BadgeClass(Program); } }
        public string StatusLabel
        {
            get
            {
                return Status == "completed" ? "Completed"
                    : Status == "ongoing" ? "Ongoing"
                    : "Upcoming";
            }
        }
        public string DateText { get { return Date.ToString("MM-dd-yy", CultureInfo.InvariantCulture); } }
        public string TimeText { get { return CoDb.FmtCompact(Start) + " - " + CoDb.FmtCompact(End); } }
        public string ShortDate { get { return Date.ToString("MMM d", CultureInfo.InvariantCulture); } }
        public string ShortTime
        {
            get
            {
                return CoDb.FmtCompact(Start).Replace(":00", "") + " - " +
                       CoDb.FmtCompact(End).Replace(":00", "");
            }
        }
    }

    public class ConflictRow
    {
        public string Id { get; set; }
        public string ScheduleId { get; set; }
        public string Title { get; set; }
        public string Program { get; set; }
        public string Section { get; set; }
        public string Room { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public bool Resolved { get; set; }
        public List<KeyValuePair<string, string>> Steps { get; set; }

        public ConflictRow()
        {
            Steps = new List<KeyValuePair<string, string>>();
        }

        public string Badge { get { return CoDb.SectionLabel(Section, Program); } }
        public string BadgeClass { get { return CoDb.BadgeClass(Program); } }
        public string DetectedText
        {
            get { return DetectedAt.ToString("M-d-yyyy", CultureInfo.InvariantCulture); }
        }
        public string ResolvedText
        {
            get
            {
                return ResolvedAt.HasValue
                    ? ResolvedAt.Value.ToString("M-d-yyyy", CultureInfo.InvariantCulture)
                    : "-------";
            }
        }
        public int StepsDone { get { return Resolved ? Steps.Count : Math.Max(0, Steps.Count - 1); } }
    }

    public class ActivityRow
    {
        public DateTime At { get; set; }
        public string Text { get; set; }
    }

    /// <summary>What the Coordinator pages show, read from MongoDB.</summary>
    public class CoordinatorData
    {
        public List<SchedRow> Schedules { get; private set; }
        public List<DefenseRow> Defenses { get; private set; }
        public List<ConflictRow> Conflicts { get; private set; }
        public List<ActivityRow> Recent { get; private set; }
        public List<string> FacultyNames { get; private set; }
        public List<string> RoomNames { get; private set; }

        public CoordinatorData()
        {
            Schedules = new List<SchedRow>();
            Defenses = new List<DefenseRow>();
            Conflicts = new List<ConflictRow>();
            Recent = new List<ActivityRow>();
            FacultyNames = new List<string>();
            RoomNames = new List<string>();
        }

        public int ToSubmit { get { return Schedules.Count(s => s.Status == "to-submit"); } }
        public int UnderReview { get { return Conflicts.Count(c => !c.Resolved); } }
        public int TotalDefenses { get { return Defenses.Count; } }
        public IEnumerable<DefenseRow> Upcoming
        {
            get { return Defenses.Where(d => d.Status != "completed").OrderBy(d => d.Date).ThenBy(d => d.Start); }
        }

        /// <param name="generate">Generate schedules for titles that have no schedule yet.</param>
        public static CoordinatorData Load(bool generate, bool onlySubsystem2 = false)
        {
            if (generate)
            {
                try { ScheduleGenerator.EnsureGenerated(); }
                catch { /* show what is already there; the next page load tries again */ }
            }

            var data = new CoordinatorData();
            DateTime now = NowManila();

            List<BsonDocument> docs = Schedules().Find(new BsonDocument()).ToList();

            if (onlySubsystem2)
            {
                HashSet<string> subsystem2Ids = Subsystem2Manuscripts.SubmissionIds();
                docs = docs
                    .Where(d => subsystem2Ids.Contains(Str(d, "titleId")))
                    .ToList();
            }

            // Resolve legacy schedule records that do not contain their own title.
            var titleOids = docs
                .Where(d => Str(d, "title") == "")
                .Select(d => ParseId(Str(d, "titleId")))
                .Where(o => o.HasValue)
                .Select(o => o.Value)
                .ToList();

            var titles = new Dictionary<string, BsonDocument>();
            if (titleOids.Count > 0)
            {
                foreach (BsonDocument t in Titles()
                    .Find(Builders<BsonDocument>.Filter.In("_id", titleOids))
                    .ToList())
                {
                    titles[DocId(t)] = t;
                }
            }

            foreach (BsonDocument d in docs)
            {
                string status = CoDb.CoStatus(d);
                BsonDocument t;
                string tid = Str(d, "titleId");
                bool haveTitle = tid != "" && titles.TryGetValue(tid, out t);
                t = haveTitle ? titles[tid] : null;

                string title = Str(d, "title") != ""
                    ? Str(d, "title")
                    : (t == null ? "" : Str(t, "title"));

                if (title == "") continue;

                string program = CoDb.ProgramCode(
                    Str(d, "program") != "" ? Str(d, "program") : (t == null ? "" : Str(t, "program")));

                string section = Str(d, "section") != ""
                    ? Str(d, "section")
                    : (t == null ? "" : Str(t, "section"));

                string author = Str(d, "manager") != ""
                    ? Str(d, "manager")
                    : (t == null ? "" : Str(t, "projectManager"));

                string adviser = Str(d, "adviser") != ""
                    ? Str(d, "adviser")
                    : (t == null ? "" : Str(t, "adviser"));

                DateTime? day = CoDb.ParseDay(Str(d, "date"));

                if (status != null)
                {
                    data.Schedules.Add(new SchedRow
                    {
                        Id = DocId(d),
                        Title = title,
                        Author = author,
                        Adviser = adviser,
                        Program = program,
                        Section = section,
                        Date = day,
                        Status = status,
                        Declined = CoDb.StatusOf(d) == "declined" ||
                                   CoDb.StatusOf(d) == "rejected"
                    });
                }

                string raw = CoDb.StatusOf(d);
                int? st = Minutes(d, "start");
                int? en = Minutes(d, "end");

                if ((raw == "approved" || raw == "completed" || raw == "complete") &&
                    day.HasValue && st.HasValue && en.HasValue)
                {
                    DateTime from = day.Value.AddMinutes(st.Value);
                    DateTime to = day.Value.AddMinutes(en.Value);
                    string defenseStatus = raw != "approved" || to < now
                        ? "completed"
                        : (from <= now ? "ongoing" : "upcoming");

                    string room = Str(d, "roomName");

                    data.Defenses.Add(new DefenseRow
                    {
                        Id = DocId(d),
                        Title = title,
                        Author = author,
                        Adviser = adviser,
                        Program = program,
                        Section = section,
                        Date = day.Value,
                        Start = st.Value,
                        End = en.Value,
                        Room = room != "" ? room : "TBA",
                        Status = defenseStatus
                    });
                }
            }

            data.Schedules = data.Schedules
                .OrderBy(s => s.Status == "under-review" ? 0 : s.Status == "to-submit" ? 1 : 2)
                .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            data.Defenses = data.Defenses
                .OrderBy(d => d.Date)
                .ThenBy(d => d.Start)
                .ToList();

            // Conflicts
            var byId = data.Schedules.ToDictionary(s => s.Id, s => s);
            var roomOf = docs.ToDictionary(d => DocId(d), d => Str(d, "roomName"));

            foreach (BsonDocument c in CoDb.Conflicts().Find(new BsonDocument()).ToList())
            {
                SchedRow sr;
                string sid = Str(c, "scheduleId");
                if (!byId.TryGetValue(sid, out sr)) continue;

                var row = new ConflictRow
                {
                    Id = DocId(c),
                    ScheduleId = sid,
                    Title = sr.Title,
                    Program = sr.Program,
                    Section = sr.Section,
                    Room = roomOf.ContainsKey(sid) && roomOf[sid] != "" ? roomOf[sid] : "-",
                    Type = Str(c, "type"),
                    Description = Str(c, "description"),
                    DetectedAt = Stamp(c, "detectedAt") ?? now,
                    ResolvedAt = Stamp(c, "resolvedAt"),
                    Resolved = Str(c, "status") == "resolved"
                };

                BsonValue stepsValue;
                if (c.TryGetValue("steps", out stepsValue) &&
                    stepsValue != null &&
                    stepsValue.IsBsonArray)
                {
                    foreach (BsonValue step in stepsValue.AsBsonArray)
                    {
                        if (step.IsBsonDocument)
                        {
                            row.Steps.Add(new KeyValuePair<string, string>(
                                Str(step.AsBsonDocument, "title"),
                                Str(step.AsBsonDocument, "text")));
                        }
                    }
                }

                data.Conflicts.Add(row);
            }

            data.Conflicts = data.Conflicts
                .OrderBy(c => c.Resolved ? 1 : 0)
                .ThenByDescending(c => c.DetectedAt)
                .ToList();

            data.Recent = CoDb.Activity()
                .Find(new BsonDocument())
                .Sort(Builders<BsonDocument>.Sort.Descending("at"))
                .Limit(6)
                .ToList()
                .Select(a => new ActivityRow
                {
                    At = Stamp(a, "at") ?? now,
                    Text = Str(a, "text")
                })
                .ToList();

            // Names for the edit boxes.
            foreach (BsonDocument u in Users().Find(new BsonDocument()).ToList())
            {
                if (!IsActive(u) ||
                    Subsystem1.Areas.Admin.Controllers.AdminApiController.RoleKey(Str(u, "role")) != "panelist")
                    continue;

                data.FacultyNames.Add(PlainName(u));
            }

            data.FacultyNames.Sort(StringComparer.OrdinalIgnoreCase);

            var ctx = PlanContext.Load();
            data.RoomNames = ctx.Rooms
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return data;
        }

        // ------------------------------------------------------------------
        // One schedule, for the pop-ups
        // ------------------------------------------------------------------
        public static object Detail(string id)
        {
            ObjectId? oid = ParseId(id);
            BsonDocument d = oid.HasValue
                ? Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault()
                : null;

            if (d == null) return null;

            string titleText = Str(d, "title");

            BsonDocument t = Subsystem2Manuscripts.LatestForSchedule(
                Str(d, "titleId"),
                Str(d, "projectManagerId"),
                titleText);

            string status = CoDb.CoStatus(d) ?? "submitted";

            string program = CoDb.ProgramCode(
                Str(d, "program") != "" ? Str(d, "program") : (t == null ? "" : Str(t, "program")));

            string section = Str(d, "section") != ""
                ? Str(d, "section")
                : (t == null ? "" : Str(t, "section"));

            string raw = CoDb.StatusOf(d);

            string problem = Str(d, "problem");
            if (raw == "declined" || raw == "rejected")
                problem = "Declined by the Dean";

            int? st = Minutes(d, "start");
            int? en = Minutes(d, "end");

            var panel = new List<object>();
            BsonValue panelValue;

            if (d.TryGetValue("panelists", out panelValue) &&
                panelValue != null &&
                panelValue.IsBsonArray)
            {
                int idx = 0;

                foreach (BsonValue p in panelValue.AsBsonArray)
                {
                    if (!p.IsBsonDocument) continue;

                    BsonDocument panelist = p.AsBsonDocument;
                    string match = Str(panelist, "match").ToLowerInvariant();

                    BsonValue percentValue;
                    double? percent = panelist.TryGetValue("percent", out percentValue) &&
                                      percentValue.IsNumeric
                        ? (double?)percentValue.ToDouble()
                        : null;

                    string role = Str(panelist, "role");
                    if (role != "Chairman" && role != "Member")
                        role = idx == 0 ? "Chairman" : "Member";

                    panel.Add(new
                    {
                        name = Str(panelist, "name"),
                        match = (match == "high" || match == "medium") ? match : "low",
                        percent = percent,
                        role = role
                    });

                    idx++;
                }
            }

            string submitted = t != null && Stamp(t, "submittedAt").HasValue
                ? Stamp(t, "submittedAt").Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                : "-";

            return new
            {
                id = DocId(d),
                status = status,
                rawStatus = raw,
                editable = CoDb.IsEditable(d),
                title = Str(d, "title") != "" ? Str(d, "title") : (t == null ? "" : Str(t, "title")),
                student = Str(d, "manager") != "" ? Str(d, "manager") : (t == null ? "" : Str(t, "projectManager")),
                program = CoDb.ProgramFull(program) + (section != "" ? " · " + section : ""),
                adviser = Str(d, "adviser") != "" ? Str(d, "adviser") : (t == null ? "" : Str(t, "adviser")),
                dateSubmitted = submitted,
                problem = problem,
                keywords = string.Join(", ", CoDb.Keywords(t)),
                manuscriptUrl = t == null ? "" : Str(t, "manuscriptUrl"),
                manuscriptFileName = t == null ? "" : Str(t, "fileName"),
                panelists = panel,
                date = Str(d, "date"),
                start = st.HasValue ? CoDb.Hhmm(st.Value) : "",
                end = en.HasValue ? CoDb.Hhmm(en.Value) : "",
                room = Str(d, "roomName")
            };
        }
    }

    // ----------------------------------------------------------------------
    // Notification drawer
    // ----------------------------------------------------------------------
    public class CoNotification
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Read { get; set; }

        public string Icon
        {
            get
            {
                switch (Type)
                {
                    case "title": return "fa-file-import";
                    case "match": return "fa-sliders";
                    case "dean":
                        return Title.IndexOf("Declined", StringComparison.OrdinalIgnoreCase) >= 0
                            ? "fa-circle-xmark"
                            : "fa-circle-check";
                    case "review": return "fa-spinner";
                    case "cancel": return "fa-calendar-xmark";
                    default: return "fa-bell";
                }
            }
        }
    }

    public static class CoordinatorNotifications
    {
        private static bool indexed;

        private static void EnsureIndex()
        {
            if (indexed) return;

            try
            {
                CoDb.Notes().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Combine(
                        Builders<BsonDocument>.IndexKeys.Ascending("coordinatorId"),
                        Builders<BsonDocument>.IndexKeys.Ascending("key")),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression =
                            new BsonDocument("key", new BsonDocument("$exists", true))
                    }));

                indexed = true;
            }
            catch { }
        }

        private static void Remember(
            string me,
            string key,
            string type,
            string title,
            string text,
            DateTime createdUtc)
        {
            var filter = Builders<BsonDocument>.Filter;
            var update = Builders<BsonDocument>.Update;

            try
            {
                CoDb.Notes().UpdateOne(
                    filter.And(
                        filter.Eq("coordinatorId", me),
                        filter.Eq("key", key)),
                    update.Combine(
                        update.SetOnInsert("type", type),
                        update.SetOnInsert("title", title),
                        update.SetOnInsert("text", text),
                        update.SetOnInsert("read", false),
                        update.SetOnInsert("createdAt", createdUtc)),
                    new UpdateOptions { IsUpsert = true });
            }
            catch (MongoWriteException) { }
        }

        public static List<CoNotification> Load(BsonDocument me)
        {
            string myId = DocId(me);
            DateTime now = NowManila();
            EnsureIndex();

            // Existing student_titles notifications are left as-is.
            foreach (BsonDocument t in Titles().Find(new BsonDocument()).ToList())
            {
                DateTime? at = Stamp(t, "submittedAt");
                if (!at.HasValue || (now - at.Value).TotalDays > 30) continue;

                string section = Str(t, "section");
                Remember(
                    myId,
                    "title:" + DocId(t),
                    "title",
                    "Title Proposal Submitted",
                    "New capstone title submitted \"" + Str(t, "title") + "\"" +
                        (section != "" ? " (" + section + ")." : "."),
                    at.Value.AddHours(-8));
            }

            foreach (BsonDocument d in Schedules().Find(new BsonDocument()).ToList())
            {
                string raw = CoDb.StatusOf(d);
                string title = Str(d, "title");
                if (title == "") continue;

                DateTime? made = Stamp(d, "generatedAt") ?? Stamp(d, "createdAt");
                DateTime? changed = Stamp(d, "approvedAt") ??
                                    Stamp(d, "declinedAt") ??
                                    Stamp(d, "updatedAt") ??
                                    made;

                string key = DocId(d);

                if (raw == "generated" && made.HasValue && (now - made.Value).TotalDays <= 30)
                {
                    Remember(
                        myId, "match:" + key, "match", "Panel & Schedule Matched",
                        "New generated schedule for review: \"" + title + "\".",
                        made.Value.AddHours(-8));
                }
                else if (raw == "under-review" && made.HasValue && (now - made.Value).TotalDays <= 30)
                {
                    Remember(
                        myId, "review:" + key, "review", "Schedule Under Review",
                        "Conflict detected: \"" + title + "\" has been placed under review (" +
                            Str(d, "problem") + ").",
                        made.Value.AddHours(-8));
                }
                else if (raw == "approved" && changed.HasValue && (now - changed.Value).TotalDays <= 30)
                {
                    Remember(
                        myId, "dean-ok:" + key, "dean", "Dean Schedule Approved",
                        "The Dean approved the submitted defense schedule for \"" + title + "\".",
                        changed.Value.AddHours(-8));
                }
                else if ((raw == "declined" || raw == "rejected") &&
                         changed.HasValue && (now - changed.Value).TotalDays <= 30)
                {
                    Remember(
                        myId, "dean-no:" + key, "dean", "Dean Schedule Declined",
                        "The Dean declined the submitted defense schedule for \"" + title + "\".",
                        changed.Value.AddHours(-8));
                }
                else if ((raw == "cancelled" || raw == "canceled") &&
                         changed.HasValue && (now - changed.Value).TotalDays <= 30)
                {
                    Remember(
                        myId, "cancel:" + key, "cancel", "Schedule Cancellation",
                        "The defense of \"" + title + "\" was cancelled.",
                        changed.Value.AddHours(-8));
                }
            }

            return CoDb.Notes()
                .Find(Builders<BsonDocument>.Filter.Eq("coordinatorId", myId))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(40)
                .ToList()
                .Select(n =>
                {
                    BsonValue readValue;
                    bool read = n.TryGetValue("read", out readValue) &&
                                readValue != null &&
                                readValue.IsBoolean &&
                                readValue.AsBoolean;

                    return new CoNotification
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

        public static void MarkRead(string myId, string id)
        {
            ObjectId? oid = ParseId(id);
            if (!oid.HasValue) return;

            var filter = Builders<BsonDocument>.Filter;
            CoDb.Notes().UpdateOne(
                filter.And(
                    filter.Eq("_id", oid.Value),
                    filter.Eq("coordinatorId", myId)),
                Builders<BsonDocument>.Update.Set("read", true));
        }
    }
}