using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using static Subsystem1.Data.SharedDb;

namespace Subsystem1.Areas.Dean.Models
{
    // ------------------------------------------------------------------
    // What the Dean reads / writes in Sia_set1_db:
    //   defense_schedules   (shared)  the Coordinator sets status "submitted"; the Dean sets
    //                       status "approved" + approvedAt + approvedBy + approvedById.
    //                       Once approved, the Student / Adviser / Panelist / Campus Director see it.
    //   dean_notifications  (new)     { deanId, key, scheduleId, title, text, read, createdAt }
    // Activity and counts are worked out from defense_schedules itself.
    // ------------------------------------------------------------------
    internal class DeanSched
    {
        public BsonDocument Doc;
        public string Id, Title, Program, Section, Author, Adviser, Room, Raw, SubmittedBy;
        public DateTime? Day, SubmittedAt, ApprovedAt;
        public int? Start, End;
        public List<string> Panel = new List<string>();

        public bool Submitted { get { return Raw == "submitted"; } }
        public bool Approved { get { return Raw == "approved" || Raw == "completed" || Raw == "complete"; } }
        public string Badge { get { return CoDb.SectionLabel(Section, Program); } }

        // completed | ongoing | upcoming  (only for approved schedules)
        public string DefenseStatus(DateTime now)
        {
            if (Raw != "approved") return "completed";
            if (!Day.HasValue || !Start.HasValue || !End.HasValue) return "upcoming";
            DateTime from = Day.Value.AddMinutes(Start.Value), to = Day.Value.AddMinutes(End.Value);
            return to < now ? "completed" : (from <= now ? "ongoing" : "upcoming");
        }
    }

    public static class DeanData
    {
        private static readonly object Gate = new object();

        private static string Pick(BsonDocument d, string field, BsonDocument t, string tField)
        {
            string v = Str(d, field);
            if (v != "") return v;
            return t == null ? "" : Str(t, tField);
        }

        private static List<DeanSched> LoadAll()
        {
            var result = new List<DeanSched>();
            var wanted = new BsonArray { "submitted", "approved", "completed", "complete" };
            List<BsonDocument> docs = Schedules().Find(Builders<BsonDocument>.Filter.In("status", wanted)).ToList();

            var titleOids = docs.Select(d => ParseId(Str(d, "titleId"))).Where(o => o.HasValue).Select(o => o.Value).Distinct().ToList();
            var titles = new Dictionary<string, BsonDocument>();
            if (titleOids.Count > 0)
            {
                foreach (BsonDocument t in Titles().Find(Builders<BsonDocument>.Filter.In("_id", titleOids)).ToList()) titles[DocId(t)] = t;
            }

            foreach (BsonDocument d in docs)
            {
                BsonDocument t;
                if (!titles.TryGetValue(Str(d, "titleId"), out t)) t = null;

                string title = Pick(d, "title", t, "title");
                if (title == "") continue;

                var s = new DeanSched
                {
                    Doc = d, Id = DocId(d), Title = title,
                    Program = CoDb.ProgramCode(Pick(d, "program", t, "program")),
                    Section = Pick(d, "section", t, "section"),
                    Author = Pick(d, "manager", t, "projectManager"),
                    Adviser = Pick(d, "adviser", t, "adviser"),
                    Room = Str(d, "roomName"),
                    Raw = CoDb.StatusOf(d),
                    SubmittedBy = Str(d, "submittedBy"),
                    Day = CoDb.ParseDay(Str(d, "date")),
                    Start = Minutes(d, "start"), End = Minutes(d, "end"),
                    SubmittedAt = Stamp(d, "submittedAt"), ApprovedAt = Stamp(d, "approvedAt")
                };

                BsonValue pv;
                if (d.TryGetValue("panelists", out pv) && pv != null && pv.IsBsonArray)
                {
                    foreach (BsonValue p in pv.AsBsonArray)
                    {
                        if (p.IsBsonDocument && Str(p.AsBsonDocument, "name") != "") s.Panel.Add(Str(p.AsBsonDocument, "name"));
                    }
                }
                result.Add(s);
            }
            return result;
        }

        private static string DateText(DateTime? d)
        {
            return d.HasValue ? d.Value.ToString("MM-dd-yy", CultureInfo.InvariantCulture) : "-------";
        }

        private static string TimeText(DeanSched s)
        {
            if (!s.Start.HasValue || !s.End.HasValue) return "-------";
            return CoDb.FmtCompact(s.Start.Value) + " - " + CoDb.FmtCompact(s.End.Value);
        }

        private static string FirstField(BsonDocument document, params string[] names)
        {
            if (document == null) return "";
            foreach (string name in names)
            {
                string value = Str(document, name);
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }
            return "";
        }

        private static DeanManuscriptFile ResolveManuscriptFile(BsonDocument schedule, BsonDocument legacyTitle)
        {
            if (schedule == null) return null;
            string titleId = Str(schedule, "titleId");
            BsonDocument source = Subsystem2Manuscripts.ForScheduleSubmission(titleId);
            string fileName = FirstField(source, "fileName", "FileName");
            string url = FirstField(source, "manuscriptUrl", "ManuscriptUrl");

            if (String.IsNullOrWhiteSpace(url))
            {
                string publicId = FirstField(source, "cloudinaryPublicId", "CloudinaryPublicId");
                if (String.IsNullOrWhiteSpace(publicId)) publicId = FirstField(source, "filePath", "filepath", "FilePath");
                string resourceType = FirstField(source, "cloudinaryResourceType", "CloudinaryResourceType");
                if (String.IsNullOrWhiteSpace(resourceType) && publicId.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) resourceType = "raw";
                if (!String.IsNullOrWhiteSpace(publicId)) url = Subsystem2Manuscripts.CreatePrivateDownloadUrl(publicId, resourceType);
            }

            if (String.IsNullOrWhiteSpace(url))
            {
                fileName = FirstField(legacyTitle, "fileName", "FileName", "filename", "name");
                string publicId = FirstField(legacyTitle, "cloudinaryPublicId", "CloudinaryPublicId", "publicId", "PublicId");
                string resourceType = FirstField(legacyTitle, "cloudinaryResourceType", "CloudinaryResourceType", "resourceType", "ResourceType");
                string filePath = FirstField(legacyTitle, "filePath", "filepath", "FilePath");
                if (String.IsNullOrWhiteSpace(publicId) && filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
                    !filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    publicId = filePath;
                if (String.IsNullOrWhiteSpace(resourceType) && publicId.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) resourceType = "raw";
                if (!String.IsNullOrWhiteSpace(publicId)) url = Subsystem2Manuscripts.CreatePrivateDownloadUrl(publicId, resourceType);
                if (String.IsNullOrWhiteSpace(url)) url = FirstField(legacyTitle, "manuscriptUrl", "ManuscriptUrl", "cloudinaryUrl", "CloudinaryUrl", "fileUrl", "FileUrl");
                if (String.IsNullOrWhiteSpace(fileName) && !String.IsNullOrWhiteSpace(publicId)) fileName = publicId.Substring(publicId.LastIndexOf('/') + 1);
                if (String.IsNullOrWhiteSpace(fileName) && !String.IsNullOrWhiteSpace(filePath) && Uri.IsWellFormedUriString(filePath, UriKind.Absolute))
                {
                    Uri fileUri;
                    if (Uri.TryCreate(filePath, UriKind.Absolute, out fileUri)) fileName = fileUri.Segments.LastOrDefault() ?? "";
                }
            }

            Uri parsed;
            bool allowedHost = Uri.TryCreate(url, UriKind.Absolute, out parsed) && parsed.Scheme == Uri.UriSchemeHttps &&
                (parsed.Host.Equals("api.cloudinary.com", StringComparison.OrdinalIgnoreCase) || parsed.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase));
            if (!allowedHost || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return null;
            return new DeanManuscriptFile { Url = url, FileName = fileName };
        }

        public static DeanManuscriptFile ManuscriptFile(string scheduleId)
        {
            ObjectId? oid = ParseId(scheduleId);
            BsonDocument schedule = oid.HasValue ? Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
            if (schedule == null) return null;
            string status = CoDb.StatusOf(schedule);
            if (status != "submitted" && status != "approved" && status != "completed" && status != "complete") return null;
            ObjectId? titleId = ParseId(Str(schedule, "titleId"));
            BsonDocument title = titleId.HasValue ? Titles().Find(Builders<BsonDocument>.Filter.Eq("_id", titleId.Value)).FirstOrDefault() : null;
            return ResolveManuscriptFile(schedule, title);
        }

        private static string Ago(DateTime at, DateTime now)
        {
            TimeSpan span = now - at;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " min ago";
            if (span.TotalHours < 24) return (int)span.TotalHours + " hr ago";
            if (span.TotalDays < 2) return "Yesterday";
            return at.ToString("MMM d", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- dashboard
        public static DashboardViewModel Dashboard()
        {
            List<DeanSched> all = LoadAll();
            DateTime now = NowManila();
            var vm = new DashboardViewModel();

            vm.ToApprove = all.Where(s => s.Submitted)
                .OrderBy(s => s.SubmittedAt ?? DateTime.MaxValue).ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(s => new DashboardRow
                {
                    Id = s.Id, Title = s.Title, Section = s.Badge, Author = s.Author, Adviser = s.Adviser, Date = DateText(s.Day)
                }).ToList();

            vm.Upcoming = all.Where(s => s.Approved && s.Day.HasValue && s.Start.HasValue && s.End.HasValue && s.DefenseStatus(now) != "completed")
                .OrderBy(s => s.Day.Value).ThenBy(s => s.Start.Value)
                .Take(3)
                .Select(s => new DashboardRow
                {
                    Id = s.Id, Title = s.Title, Section = s.Badge, Author = s.Author, Adviser = s.Adviser,
                    Room = s.Room != "" ? s.Room : "-", Date = DateText(s.Day), Time = TimeText(s).Replace(":00", "")
                }).ToList();

            vm.TotalDefenses = all.Count(s => s.Approved);

            var events = new List<KeyValuePair<DateTime, string>>();
            foreach (DeanSched s in all)
            {
                if (s.SubmittedAt.HasValue)
                    events.Add(new KeyValuePair<DateTime, string>(s.SubmittedAt.Value, "Defense schedule submitted for \"" + s.Title + "\""));
                if (s.ApprovedAt.HasValue)
                    events.Add(new KeyValuePair<DateTime, string>(s.ApprovedAt.Value, "You approved the defense of \"" + s.Title + "\""));
            }
            vm.RecentActivities = events.OrderByDescending(e => e.Key).Take(6)
                .Select(e => new ActivityRow { Time = Ago(e.Key, now), Text = e.Value }).ToList();
            return vm;
        }

        // ---------------------------------------------------------------- schedule approval list
        public static List<ScheduleListItem> Approvals()
        {
            return LoadAll()
                .OrderBy(s => s.Submitted ? 0 : 1)
                .ThenBy(s => s.Day ?? DateTime.MaxValue)
                .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                .Select(s => new ScheduleListItem
                {
                    Id = s.Id, Title = s.Title, Section = s.Badge, Program = s.Program, ProjectManager = s.Author, Adviser = s.Adviser,
                    Date = s.Day ?? DateTime.MinValue, HasDate = s.Day.HasValue, Status = s.Approved ? "Approved" : "To Approve"
                }).ToList();
        }

        // one schedule for the pop-up
        public static ScheduleDetailsVM Detail(string id)
        {
            ObjectId? oid = ParseId(id);
            BsonDocument d = oid.HasValue ? Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
            if (d == null) return null;
            string raw = CoDb.StatusOf(d);
            if (raw != "submitted" && raw != "approved" && raw != "completed" && raw != "complete") return null;

            ObjectId? toid = ParseId(Str(d, "titleId"));
            BsonDocument t = toid.HasValue ? Titles().Find(Builders<BsonDocument>.Filter.Eq("_id", toid.Value)).FirstOrDefault() : null;

            string program = CoDb.ProgramCode(Pick(d, "program", t, "program"));
            string section = Pick(d, "section", t, "section");
            DateTime? day = CoDb.ParseDay(Str(d, "date"));
            int? st = Minutes(d, "start"), en = Minutes(d, "end");
            DateTime? submitted = Stamp(d, "submittedAt");
            if (!submitted.HasValue && t != null) submitted = Stamp(t, "submittedAt");

            var panel = new List<PanelistVM>();
            BsonValue pv;
            if (d.TryGetValue("panelists", out pv) && pv != null && pv.IsBsonArray)
            {
                int idx = 0;
                foreach (BsonValue p in pv.AsBsonArray)
                {
                    if (!p.IsBsonDocument) continue;
                    BsonDocument pd = p.AsBsonDocument;
                    string m = Str(pd, "match").ToLowerInvariant();
                    BsonValue pc;
                    string role = Str(pd, "role");
                    if (role != "Chairman" && role != "Member") role = idx == 0 ? "Chairman" : "Member";   // older records
                    panel.Add(new PanelistVM
                    {
                        Name = Str(pd, "name"),
                        Expertise = m == "high" ? "High" : m == "medium" ? "Medium" : "Low",
                        Percent = pd.TryGetValue("percent", out pc) && pc.IsNumeric ? (double?)pc.ToDouble() : null,
                        Role = role
                    });
                    idx++;
                }
            }

            bool approved = raw != "submitted";
            DeanManuscriptFile manuscriptFile = ResolveManuscriptFile(d, t);
            return new ScheduleDetailsVM
            {
                Id = DocId(d),
                ProjectTitle = Pick(d, "title", t, "title"),
                DateSubmitted = submitted.HasValue ? submitted.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "-",
                Status = approved ? "Approved" : "For Approval",
                Student = Pick(d, "manager", t, "projectManager"),
                ProgramSection = CoDb.SectionLabel(section, program),
                Adviser = Pick(d, "adviser", t, "adviser"),
                Abstract = t == null ? "" : Str(t, "abstract"),
                Keywords = string.Join(", ", CoDb.Keywords(t)),
                HasManuscriptPdf = manuscriptFile != null,
                Panelists = panel,
                Time = st.HasValue && en.HasValue ? CoDb.FmtCompact(st.Value) + " - " + CoDb.FmtCompact(en.Value) : "-",
                DefenseDate = day.HasValue ? day.Value.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) : "-",
                Room = Str(d, "roomName") != "" ? Str(d, "roomName") : "-"
            };
        }

        /// <summary>Final approval. Returns an error message, or null when it was approved.</summary>
        public static string Approve(string id, BsonDocument dean)
        {
            lock (Gate)
            {
                ObjectId? oid = ParseId(id);
                BsonDocument sched = oid.HasValue ? Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
                if (sched == null) return "Schedule not found.";

                string raw = CoDb.StatusOf(sched);
                if (raw == "approved" || raw == "completed" || raw == "complete") return "This schedule is already approved.";
                if (raw != "submitted") return "This schedule is not waiting for approval.";

                // last check: nothing else may have taken the room or the people since it was submitted
                var names = new List<string>();
                BsonValue pv;
                if (sched.TryGetValue("panelists", out pv) && pv != null && pv.IsBsonArray)
                {
                    foreach (BsonValue p in pv.AsBsonArray) if (p.IsBsonDocument) names.Add(Str(p.AsBsonDocument, "name"));
                }
                var input = new PlanInput
                {
                    Panelists = names,
                    Date = Str(sched, "date"),
                    Start = CoDb.Hhmm(Minutes(sched, "start") ?? 0),
                    End = CoDb.Hhmm(Minutes(sched, "end") ?? 0),
                    Room = Str(sched, "roomName")
                };
                PlanCheck chk = ScheduleGenerator.Check(sched, input, PlanContext.Load());
                if (chk.Error != null) return "Cannot approve yet: " + chk.Error + " Please ask the Capstone Coordinator to fix the schedule.";

                DateTime now = DateTime.UtcNow;
                string who = PlainName(dean);
                var f = Builders<BsonDocument>.Filter;
                var u = Builders<BsonDocument>.Update;
                UpdateResult res = Schedules().UpdateOne(
                    f.And(f.Eq("_id", oid.Value), f.Eq("status", "submitted")),
                    u.Combine(u.Set("status", "approved"), u.Set("approvedAt", now), u.Set("approvedBy", who),
                              u.Set("approvedById", DocId(dean)), u.Set("updatedAt", now)));
                if (res.ModifiedCount == 0) return "This schedule was already handled.";

                CoDb.Log("Dean approved the defense schedule for \"" + Str(sched, "title") + "\"");
                return null;
            }
        }

        // ---------------------------------------------------------------- defense schedule page
        public static DefenseScheduleViewModel Defenses(string search, string program, string status, string adviser)
        {
            DateTime now = NowManila();
            var all = LoadAll().Where(s => s.Approved && s.Day.HasValue && s.Start.HasValue && s.End.HasValue)
                .OrderBy(s => s.Day.Value).ThenBy(s => s.Start.Value)
                .Select(s => new DefenseScheduleItem
                {
                    Id = s.Id, Manuscript = s.Title, Section = s.Badge, Author = s.Author, Adviser = s.Adviser,
                    Date = s.Day.Value, StartTime = TimeSpan.FromMinutes(s.Start.Value), EndTime = TimeSpan.FromMinutes(s.End.Value),
                    Room = s.Room != "" ? s.Room : "-", Panelists = s.Panel,
                    Status = s.DefenseStatus(now) == "completed" ? "Completed" : s.DefenseStatus(now) == "ongoing" ? "Ongoing" : "Upcoming"
                }).ToList();

            IEnumerable<DefenseScheduleItem> q = all;
            if (!string.IsNullOrWhiteSpace(search))
            {
                string k = search.Trim().ToLowerInvariant();
                q = q.Where(x => x.Manuscript.ToLowerInvariant().Contains(k) || x.Author.ToLowerInvariant().Contains(k)
                              || x.Adviser.ToLowerInvariant().Contains(k) || x.Panelists.Any(p => p.ToLowerInvariant().Contains(k)));
            }
            if (!string.IsNullOrEmpty(program) && program != "All") q = q.Where(x => x.Program == program);
            if (!string.IsNullOrEmpty(status) && status != "All") q = q.Where(x => x.Status == status);
            if (!string.IsNullOrEmpty(adviser) && adviser != "All") q = q.Where(x => x.Adviser == adviser);

            return new DefenseScheduleViewModel
            {
                Items = q.ToList(), Search = search, Program = program ?? "All", Status = status ?? "All", Adviser = adviser ?? "All",
                Programs = all.Select(x => x.Program).Where(x => x != "").Distinct().OrderBy(x => x).ToList(),
                Advisers = all.Select(x => x.Adviser).Where(x => x != "").Distinct().OrderBy(x => x).ToList()
            };
        }
    }

    // ----------------------------------------------------------------------
    // Notification drawer: { deanId, key, scheduleId, title, text, read, createdAt }
    // ----------------------------------------------------------------------
    public class DeanNotification
    {
        public string Id { get; set; }
        public string ScheduleId { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Read { get; set; }
    }

    public static class DeanNotifications
    {
        private static bool indexed;

        private static IMongoCollection<BsonDocument> Notes()
        {
            return Subsystem1.Data.Db.Database.GetCollection<BsonDocument>("dean_notifications");
        }

        private static void EnsureIndex()
        {
            if (indexed) return;
            try
            {
                Notes().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Combine(
                        Builders<BsonDocument>.IndexKeys.Ascending("deanId"),
                        Builders<BsonDocument>.IndexKeys.Ascending("key")),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = new BsonDocument("key", new BsonDocument("$exists", true))
                    }));
                indexed = true;
            }
            catch { }
        }

        public static List<DeanNotification> Load(BsonDocument me)
        {
            string myId = DocId(me);
            DateTime now = NowManila();
            EnsureIndex();

            var wanted = new BsonArray { "submitted", "approved", "completed", "complete" };
            var f = Builders<BsonDocument>.Filter;
            var u = Builders<BsonDocument>.Update;

            foreach (BsonDocument d in Schedules().Find(f.In("status", wanted)).ToList())
            {
                DateTime? at = Stamp(d, "submittedAt");
                string title = Str(d, "title");
                if (!at.HasValue || title == "" || (now - at.Value).TotalDays > 30) continue;

                string by = Str(d, "submittedBy");
                if (by == "") by = "The Capstone Coordinator";
                string label = CoDb.SectionLabel(Str(d, "section"), CoDb.ProgramCode(Str(d, "program")));
                try
                {
                    Notes().UpdateOne(
                        f.And(f.Eq("deanId", myId), f.Eq("key", "sub:" + DocId(d))),
                        u.Combine(
                            u.SetOnInsert("scheduleId", DocId(d)),
                            u.SetOnInsert("title", by + " submitted a defense schedule"),
                            u.SetOnInsert("text", "Defense schedule for " + label + ", with a title \"" + title + "\""),
                            u.SetOnInsert("read", false),
                            u.SetOnInsert("createdAt", at.Value.AddHours(-8))),
                        new UpdateOptions { IsUpsert = true });
                }
                catch (MongoWriteException) { }
            }

            return Notes().Find(f.Eq("deanId", myId))
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(40)
                .ToList()
                .Select(n =>
                {
                    BsonValue rv;
                    bool read = n.TryGetValue("read", out rv) && rv != null && rv.IsBoolean && rv.AsBoolean;
                    return new DeanNotification
                    {
                        Id = DocId(n), ScheduleId = Str(n, "scheduleId"), Title = Str(n, "title"), Text = Str(n, "text"),
                        CreatedAt = Stamp(n, "createdAt") ?? now, Read = read
                    };
                }).ToList();
        }

        public static void MarkRead(string myId, string id)
        {
            ObjectId? oid = ParseId(id);
            if (!oid.HasValue) return;
            var f = Builders<BsonDocument>.Filter;
            Notes().UpdateOne(f.And(f.Eq("_id", oid.Value), f.Eq("deanId", myId)),
                Builders<BsonDocument>.Update.Set("read", true));
        }
    }
}
