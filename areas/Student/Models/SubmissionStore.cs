using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.Student.ViewModels;
using AdminApi = Subsystem1.Areas.Admin.Controllers.AdminApiController;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.Student.Models
{
    /// <summary>
    /// One student's capstone progress, read from MongoDB.
    ///   - the title, activity log and "defense completed" confirmation are saved by the student;
    ///   - date, panelists, room and approval come from the matching document in defense_schedules,
    ///     which the Coordinator / Dean modules write (see StudentDb for the layout).
    /// Load() builds a fresh snapshot for the request; nothing is shared between students.
    /// </summary>
    public class SubmissionStore
    {
        public List<ManuscriptItemDto> Titles { get; private set; }
        public List<LogItem> Log { get; private set; }
        public List<PanelistInfo> Panelists { get; private set; }

        public bool TitleSubmitted { get { return Titles.Count > 0; } }
        public DateTime? DefenseDate { get; private set; }     // date + start time
        public DateTime? DefenseEnd { get; private set; }
        public string Room { get; private set; }
        public bool DefenseScheduled { get; private set; }
        public bool DefenseComplete { get; private set; }
        public int PanelistCount { get { return Panelists.Count; } }

        /// <summary>When the tracker last changed (newest log entry), or null if nothing has happened yet.</summary>
        public DateTime? LastChanged
        {
            get { return Log.Count == 0 ? (DateTime?)null : Log.Max(l => l.Timestamp); }
        }

        public static readonly string[] Programs =
            { "BS Computer Science", "BS Information Technology", "BS Information System" };

        private SubmissionStore()
        {
            Titles = new List<ManuscriptItemDto>();
            Log = new List<LogItem>();
            Panelists = new List<PanelistInfo>();
        }

        // ------------------------------------------------------------------
        // Reading
        // ------------------------------------------------------------------
        public static SubmissionStore Load(BsonDocument me)
        {
            var s = new SubmissionStore();
            string sid = DocId(me);

            List<BsonDocument> titleDocs = Titles_().Find(Builders<BsonDocument>.Filter.Eq("studentId", sid))
                .ToList()
                .OrderByDescending(t => Stamp(t, "submittedAt") ?? DateTime.MinValue)
                .ToList();

            string managerName = PlainName(me);
            bool confirmedByStudent = false;

            foreach (BsonDocument t in titleDocs)
            {
                var keywords = new List<string>();
                BsonValue kv;
                if (t.TryGetValue("keywords", out kv) && kv != null && kv.IsBsonArray)
                {
                    foreach (BsonValue k in kv.AsBsonArray) if (k.IsString) keywords.Add(k.AsString);
                }

                BsonValue done;
                if (t.TryGetValue("defenseCompleted", out done) && done != null && done.IsBoolean && done.AsBoolean)
                {
                    confirmedByStudent = true;
                }

                s.Titles.Add(new ManuscriptItemDto
                {
                    Id = DocId(t),
                    Title = Str(t, "title"),
                    Abstract = Str(t, "abstract"),
                    Keywords = keywords,
                    Authors = Str(t, "authors"),
                    DatePublished = Stamp(t, "datePublished"),
                    Program = Str(t, "program"),
                    Section = Str(t, "section") != "" ? Str(t, "section") : Str(me, "section"),
                    Adviser = Str(t, "adviser"),
                    ProjectManager = Str(t, "projectManager") != "" ? Str(t, "projectManager") : managerName,
                    DateAndSubmissionTime = Stamp(t, "submittedAt") ?? NowManila(),
                    Status = "Submitted"
                });
            }

            // ---- the matching defense schedule (only once the Dean has approved it) ----
            BsonDocument sched = s.Titles.Count == 0 ? null : FindSchedule(sid, s.Titles.Select(t => t.Id).ToList());
            bool schedCompleted = false;

            DefenseInfo info = sched == null ? null : DefenseInfo.Parse(sched, RoomNameById);
            if (info != null)
            {
                s.DefenseDate = info.Start;
                s.DefenseEnd = info.End;
                s.Panelists = info.Panelists;
                s.Room = info.Room;
                schedCompleted = info.CompletedFlag;
                s.DefenseScheduled = info.IsScheduled;
            }

            s.DefenseComplete = s.TitleSubmitted && (confirmedByStudent || schedCompleted);

            string status = s.DefenseComplete ? "Completed" : (s.DefenseDate.HasValue ? "Processing" : "Submitted");
            foreach (ManuscriptItemDto t in s.Titles) t.Status = status;

            // ---- activity log: what the student did + what the schedule says ----
            foreach (BsonDocument a in Activity().Find(Builders<BsonDocument>.Filter.Eq("studentId", sid)).ToList())
            {
                s.Log.Add(new LogItem
                {
                    Step = a.Contains("step") && a["step"].IsInt32 ? a["step"].AsInt32 : 0,
                    Timestamp = Stamp(a, "ts") ?? NowManila(),
                    Text = Str(a, "text")
                });
            }

            if (sched != null)
            {
                // Times for these come from the schedule's own approval/update time. Without one
                // the entries are left out, because a made-up time would change on every refresh.
                DateTime? when = info.Stamp;
                if (when.HasValue)
                {
                    if (s.DefenseDate.HasValue)
                    {
                        s.Log.Add(new LogItem { Step = 1, Timestamp = when.Value,
                            Text = "Date Set by **" + s.DefenseDate.Value.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
                                + "** at **" + s.DefenseDate.Value.ToString("h:mmtt", CultureInfo.InvariantCulture) + "**" });
                    }
                    foreach (PanelistInfo p in s.Panelists)
                    {
                        s.Log.Add(new LogItem { Step = 2, Timestamp = when.Value,
                            Text = "**" + p.Name + "** has been assigned as **" + p.Role + "**" });
                    }
                    if (s.Room != null)
                    {
                        s.Log.Add(new LogItem { Step = 3, Timestamp = when.Value, Text = "Room set to **" + s.Room + "**" });
                    }
                    if (s.DefenseScheduled)
                    {
                        s.Log.Add(new LogItem { Step = 4, Timestamp = when.Value, Text = "Status changed to **\"Defense Scheduled\"**" });
                    }
                    if (schedCompleted && !confirmedByStudent)
                    {
                        s.Log.Add(new LogItem { Step = 5, Timestamp = when.Value, Text = "Defense marked as **Complete**" });
                    }
                }
            }

            s.Log = s.Log.OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Step).ToList();
            return s;
        }

        // The newest schedule for this student that the Dean approved (cancelled ones are ignored).
        private static BsonDocument FindSchedule(string studentId, List<string> titleIds)
        {
            var f = Builders<BsonDocument>.Filter;
            var filters = new List<FilterDefinition<BsonDocument>> { f.Eq("projectManagerId", studentId) };
            foreach (string id in titleIds) filters.Add(f.Eq("titleId", id));

            return Schedules().Find(f.Or(filters))
                .ToList()
                .Where(DefenseInfo.IsVisibleStatus)
                .OrderByDescending(d => Str(d, "date"))
                .FirstOrDefault();
        }

        private static string RoomNameById(string id)
        {
            ObjectId? rid = ParseId(id);
            if (!rid.HasValue) return null;
            BsonDocument room = Rooms().Find(Builders<BsonDocument>.Filter.Eq("_id", rid.Value)).FirstOrDefault();
            return room == null ? null : Str(room, "name");
        }

        private static IMongoCollection<BsonDocument> Titles_() { return StudentDb.Titles(); }

        public List<StepItem> BuildSteps()
        {
            return new List<StepItem>
            {
                new StepItem { Label = "Title Submitted", Detail = TitleSubmitted ? "Complete" : "Pending", Done = TitleSubmitted },
                new StepItem { Label = "Date Set", Detail = DefenseDate.HasValue ? DefenseDate.Value.ToString("MMM. d, yyyy - h:mmtt", CultureInfo.InvariantCulture) : "Pending", Done = DefenseDate.HasValue },
                new StepItem { Label = "Panelist Assigned", Detail = PanelistCount > 0 ? PanelistCount + " Panelist Assigned" : "Pending", Done = PanelistCount > 0 },
                new StepItem { Label = "Room Set", Detail = Room ?? "Pending", Done = Room != null },
                new StepItem { Label = "Defense Scheduled", Detail = DefenseScheduled ? "Complete" : "Pending", Done = DefenseScheduled },
                new StepItem { Label = "Defense Complete", Detail = DefenseComplete ? "Complete" : "Pending", Done = DefenseComplete },
            };
        }

        // ------------------------------------------------------------------
        // Writing (only what the student is allowed to change)
        // ------------------------------------------------------------------

        private static bool indexed;

        private static void EnsureIndex()
        {
            if (indexed) return;
            try
            {
                // One title per student, even if the button is double-clicked.
                StudentDb.Titles().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("studentId"),
                    new CreateIndexOptions { Unique = true }));
                indexed = true;
            }
            catch
            {
                // The check in AddTitle still applies; try the index again next time.
            }
        }

        /// <summary>Saves the capstone title. Returns an error message, or null when it was saved.</summary>
        public static string AddTitle(BsonDocument me, StudentTitleInput input)
        {
            string sid = DocId(me);

            if (StudentDb.Titles().Find(Builders<BsonDocument>.Filter.Eq("studentId", sid)).FirstOrDefault() != null)
            {
                return "You have already submitted a capstone title.";
            }

            // The adviser must be a real, active adviser account.
            BsonDocument adviser = null;
            ObjectId? aid = ParseId(input.AdvisorId);
            if (aid.HasValue)
            {
                adviser = Users().Find(Builders<BsonDocument>.Filter.Eq("_id", aid.Value)).FirstOrDefault();
            }
            if (adviser == null || !IsActive(adviser) || !IsFaculty(adviser))
            {
                return "Please choose a valid adviser.";
            }

            // The program comes from the student's account when it has one.
            string program = ProgramName(Str(me, "program"));
            if (program == "") program = Clean(input.Program);
            if (!Programs.Contains(program)) return "Please choose a valid program.";

            // The section comes from the student's account when it has one.
            string section = Str(me, "section").Trim();
            if (section == "") section = System.Text.RegularExpressions.Regex.Replace(Clean(input.Section), @"\s+", "").ToUpperInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(section, "^[A-Z0-9-]{2,20}$")) return "Please enter your section, for example SBIT-4A.";

            string title = System.Text.RegularExpressions.Regex.Replace(Clean(input.Title), @"\s+", " ");
            string abs = Clean(input.Abstract);
            string authors = Clean(input.Authors);
            if (title == "" || abs == "" || authors == "") return "Please fill in all required fields.";

            var keywords = new BsonArray(
                (input.Keywords ?? "").Split(',')
                    .Select(k => k.Trim())
                    .Where(k => k.Length > 0 && k.Length <= 60)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(15));

            string manager = PlainName(me);
            DateTime now = DateTime.UtcNow;

            var doc = new BsonDocument
            {
                { "studentId", sid },
                { "title", title },
                { "abstract", abs },
                { "keywords", keywords },
                { "authors", authors },
                { "program", program },
                { "section", section },
                { "adviserId", DocId(adviser) },
                { "adviser", PlainName(adviser) },
                { "projectManager", manager },
                { "defenseCompleted", false },
                { "submittedAt", now }
            };
            if (input.DatePublished.HasValue)
            {
                doc["datePublished"] = DateTime.SpecifyKind(input.DatePublished.Value.Date, DateTimeKind.Utc);
            }

            EnsureIndex();
            try
            {
                StudentDb.Titles().InsertOne(doc);
            }
            catch (MongoWriteException ex)
            {
                if (ex.WriteError != null && ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    return "You have already submitted a capstone title.";
                }
                throw;
            }

            AddLog(sid, 0, "Capstone Title submitted by **" + manager + "**");

            // Hand the title to the scheduling system (Capstone Coordinator). Never blocks the student.
            try
            {
                Subsystem1.Areas.CapstoneCoordinator.Models.ScheduleGenerator.GenerateForTitle(doc["_id"].ToString());
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>"Is your defense completed?" -> Yes. Returns an error message, or null when it was saved.</summary>
        public static string CompleteDefense(BsonDocument me)
        {
            SubmissionStore s = Load(me);
            string sid = DocId(me);

            if (!s.TitleSubmitted) return "Submit a capstone title first.";
            if (s.DefenseComplete) return null;
            if (!s.DefenseScheduled) return "Your defense has not been scheduled yet.";
            if (s.DefenseEnd.HasValue && NowManila() < s.DefenseEnd.Value)
            {
                return "You can confirm your defense once it has taken place.";
            }

            var u = Builders<BsonDocument>.Update;
            StudentDb.Titles().UpdateMany(
                Builders<BsonDocument>.Filter.Eq("studentId", sid),
                u.Combine(u.Set("defenseCompleted", true), u.Set("defenseCompletedAt", DateTime.UtcNow)));

            AddLog(sid, 5, "Defense marked as **Complete**");
            return null;
        }

        private static void AddLog(string studentId, int step, string text)
        {
            try
            {
                Activity().InsertOne(new BsonDocument
                {
                    { "studentId", studentId },
                    { "step", step },
                    { "text", text },
                    { "ts", DateTime.UtcNow }
                });
            }
            catch
            {
                // Never fail the real action just because logging failed.
            }
        }
    }
}
