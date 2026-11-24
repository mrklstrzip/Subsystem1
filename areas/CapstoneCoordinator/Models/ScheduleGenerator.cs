using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using AdminApi = Subsystem1.Areas.Admin.Controllers.AdminApiController;
using static Subsystem1.Data.SharedDb;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    public class Booking
    {
        public string ScheduleId;
        public string RoomId;
        public DateTime Day;
        public int Start;
        public int End;
        public HashSet<string> People = new HashSet<string>();   // panelists + adviser
        public HashSet<string> Panel = new HashSet<string>();    // panelists only
    }

    /// <summary>Everything the scheduler needs to know about who and what is already taken.</summary>
    public class PlanContext
    {
        public Dictionary<string, BsonDocument> Panelists = new Dictionary<string, BsonDocument>();   // by user id
        public Dictionary<string, BsonDocument> People = new Dictionary<string, BsonDocument>();      // panelists + advisers
        public Dictionary<string, string> NameIndex = new Dictionary<string, string>();               // name key -> user id
        public List<EngRoom> Rooms = new List<EngRoom>();
        public List<Booking> Bookings = new List<Booking>();
        public List<BsonDocument> Classes = new List<BsonDocument>();
        public DateTime Today;

        public static PlanContext Load()
        {
            var c = new PlanContext { Today = NowManila().Date };

            foreach (BsonDocument u in Users().Find(new BsonDocument()).ToList())
            {
                if (!IsActive(u)) continue;
                string key = AdminApi.RoleKey(Str(u, "role"));
                if (key != "panelist" && key != "adviser") continue;

                string id = DocId(u);
                c.People[id] = u;
                if (key == "panelist") c.Panelists[id] = u;
                foreach (string v in FacultyNames.Variants(u)) if (!c.NameIndex.ContainsKey(v)) c.NameIndex[v] = id;
            }

            var buildings = new Dictionary<string, BsonDocument>();
            foreach (BsonDocument b in Db.Database.GetCollection<BsonDocument>("buildings").Find(new BsonDocument()).ToList()) buildings[DocId(b)] = b;
            foreach (BsonDocument r in Rooms_().Find(new BsonDocument()).ToList())
            {
                if (!CoDb.IsFree(r)) continue;
                BsonDocument b = null;
                string bid = AnyStr(r, "buildingId");
                if (bid != "" && buildings.TryGetValue(bid, out b) && !CoDb.IsFree(b)) continue;
                if (IsExcludedRoom(r, b)) continue;     // IE (Korphil) is never used for defenses
                c.Rooms.Add(new EngRoom { Id = DocId(r), Name = Str(r, "name"), Building = b == null ? "" : Str(b, "name") });
            }

            c.Classes = Db.Database.GetCollection<BsonDocument>("class_schedules").Find(new BsonDocument()).ToList();

            foreach (BsonDocument s in Schedules().Find(new BsonDocument()).ToList())
            {
                if (!CoDb.HoldsRoom(s)) continue;
                DateTime? day = CoDb.ParseDay(Str(s, "date"));
                int? st = Minutes(s, "start"), en = Minutes(s, "end");
                if (!day.HasValue || !st.HasValue || !en.HasValue) continue;

                var b = new Booking { ScheduleId = DocId(s), RoomId = AnyStr(s, "roomId"), Day = day.Value, Start = st.Value, End = en.Value };
                string adviser = Str(s, "adviserId");
                if (adviser != "") b.People.Add(adviser);

                BsonValue pv;
                if (s.TryGetValue("panelists", out pv) && pv != null && pv.IsBsonArray)
                {
                    foreach (BsonValue p in pv.AsBsonArray)
                    {
                        string pid = "";
                        if (p.IsBsonDocument)
                        {
                            pid = AnyStr(p.AsBsonDocument, "userId");
                            if (pid == "") pid = c.IdByName(Str(p.AsBsonDocument, "name"));
                        }
                        else if (p.IsString)
                        {
                            pid = c.IdByName(p.AsString);
                        }
                        if (pid != "") { b.People.Add(pid); b.Panel.Add(pid); }
                    }
                }
                c.Bookings.Add(b);
            }
            return c;
        }

        private static IMongoCollection<BsonDocument> Rooms_() { return SharedDb.Rooms(); }

        // Rooms of the IE (Korphil) building are not used when generating or editing a schedule.
        private static bool IsExcludedRoom(BsonDocument room, BsonDocument building)
        {
            string[] texts =
            {
                Str(room, "name"),
                building == null ? "" : Str(building, "name"),
                building == null ? "" : Str(building, "code")
            };
            foreach (string t in texts)
            {
                string v = (t ?? "").Trim();
                if (v.IndexOf("korphil", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (string.Equals(v, "IE", StringComparison.OrdinalIgnoreCase)) return true;
                if (Regex.IsMatch(v, @"^IE\d", RegexOptions.IgnoreCase)) return true;   // room names such as IE304
                if (v.StartsWith("IE ", StringComparison.OrdinalIgnoreCase) || v.StartsWith("IE-", StringComparison.OrdinalIgnoreCase)
                    || v.StartsWith("IE(", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>roomId -> building name, for every room (including rooms that are not used for generation).</summary>
        public static Dictionary<string, string> BuildingNamesByRoom()
        {
            var names = new Dictionary<string, string>();
            var buildings = new Dictionary<string, string>();
            foreach (BsonDocument b in Db.Database.GetCollection<BsonDocument>("buildings").Find(new BsonDocument()).ToList()) buildings[DocId(b)] = Str(b, "name");
            foreach (BsonDocument r in Rooms_().Find(new BsonDocument()).ToList())
            {
                string name;
                if (buildings.TryGetValue(AnyStr(r, "buildingId"), out name)) names[DocId(r)] = name;
            }
            return names;
        }

        public string IdByName(string name)
        {
            string id;
            return NameIndex.TryGetValue(FacultyNames.Key(name), out id) ? id : "";
        }

        private static bool Overlap(int s1, int e1, int s2, int e2) { return s1 < e2 && s2 < e1; }

        public bool RoomBusy(string roomId, DateTime day, int s, int e, string exclude)
        {
            return Bookings.Any(b => b.ScheduleId != exclude && b.RoomId == roomId && b.Day == day && Overlap(s, e, b.Start, b.End));
        }

        // another defense this person is part of (as panelist or adviser) at the same time
        public Booking PersonBooking(string id, DateTime day, int s, int e, string exclude)
        {
            return Bookings.FirstOrDefault(b => b.ScheduleId != exclude && b.Day == day && b.People.Contains(id) && Overlap(s, e, b.Start, b.End));
        }

        public BsonDocument ClassClash(string id, DateTime day, int s, int e)
        {
            string dayName = day.DayOfWeek.ToString();
            foreach (BsonDocument c in Classes)
            {
                if (Str(c, "userId") != id || Str(c, "day") != dayName) continue;
                int cs = Minutes(c, "start") ?? 0, ce = Minutes(c, "end") ?? 0;
                if (Overlap(s, e, cs, ce)) return c;
            }
            return null;
        }

        public bool SectionClassClash(string section, DateTime day, int s, int e)
        {
            string wanted = SectionKey(section);
            if (wanted == "") return false;
            string dayName = day.DayOfWeek.ToString();
            foreach (BsonDocument c in Classes)
            {
                if (!String.Equals(Str(c, "day"), dayName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!ClassSections(c).Any(value => SectionKey(value) == wanted)) continue;
                int cs = Minutes(c, "start") ?? 0, ce = Minutes(c, "end") ?? 0;
                if (Overlap(s, e, cs, ce)) return true;
            }
            return false;
        }

        private static IEnumerable<string> ClassSections(BsonDocument schedule)
        {
            foreach (string field in new[] { "section", "sectionName", "sectionCode" })
            {
                BsonValue value;
                if (!schedule.TryGetValue(field, out value) || value == null || value.IsBsonNull) continue;
                if (value.IsString) yield return value.AsString;
                else if (value.IsBsonArray)
                    foreach (BsonValue item in value.AsBsonArray) if (item.IsString) yield return item.AsString;
            }
            BsonValue sections;
            if (schedule.TryGetValue("sections", out sections) && sections != null && sections.IsBsonArray)
                foreach (BsonValue item in sections.AsBsonArray) if (item.IsString) yield return item.AsString;
        }

        private static string SectionKey(string section)
        {
            string key = Regex.Replace((section ?? "").Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
            if (key.StartsWith("SBIT", StringComparison.Ordinal)) key = "BSIT" + key.Substring(4);
            else if (key.StartsWith("SBCS", StringComparison.Ordinal)) key = "BSCS" + key.Substring(4);
            else if (key.StartsWith("SBIS", StringComparison.Ordinal)) key = "BSIS" + key.Substring(4);
            return key;
        }

        public int DayLoad(string id, DateTime day) { return Bookings.Count(b => b.Day == day && b.Panel.Contains(id)); }
        public int TotalLoad(string id) { return Bookings.Count(b => b.Day >= Today && b.Panel.Contains(id)); }

        public string NameOf(string id)
        {
            BsonDocument u;
            return People.TryGetValue(id, out u) ? PlainName(u) : "A panelist";
        }
    }

    public class PlanInput
    {
        public List<string> Panelists { get; set; }
        public string Date { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public string Room { get; set; }
    }

    public class PlanCheck
    {
        public string Error;
        public List<string> Soft = new List<string>();     // class overlaps: allowed after the coordinator confirms
        public DateTime Day;
        public int Start;
        public int End;
        public EngRoom Room;
        public List<BsonDocument> Panel = new List<BsonDocument>();
    }

    public static class ScheduleGenerator
    {
        private static readonly object Gate = new object();
        private static bool indexed;
        private const int PdfKeywordParserVersion = 3;

        private static int KeywordParserVersion(BsonDocument doc)
        {
            BsonValue value;
            return doc != null && doc.TryGetValue("keywordParserVersion", out value) && value != null && value.IsNumeric
                ? value.ToInt32()
                : 0;
        }

        private static bool IsEligiblePanelist(BsonDocument user, string adviserId)
        {
            if (user == null) return false;
            string id = DocId(user);
            return id != "" && !String.Equals(id, adviserId ?? "", StringComparison.OrdinalIgnoreCase) &&
                   AdminApi.RoleKey(Str(user, "role")) == "panelist";
        }

        private static void EnsureIndex()
        {
            if (indexed) return;
            try
            {
                CoDb.Claims().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("titleId"),
                    new CreateIndexOptions<BsonDocument> { Unique = true }));
                indexed = true;
            }
            catch
            {
                // still works inside one server process thanks to the lock; try again next time
            }
        }

        private static List<string> SplitAuthors(string authors)
        {
            return Regex.Split(authors ?? "", @"\r?\n|,|;").Select(a => a.Trim()).Where(a => a != "").ToList();
        }

        private static BsonDocument ManagerOf(BsonDocument title)
        {
            ObjectId? oid = ParseId(Str(title, "studentId"));
            return oid.HasValue ? Users().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
        }

        /// <summary>Schedules every title that has no schedule yet. Returns how many were handled.</summary>
        public static int EnsureGenerated()
        {
            lock (Gate)
            {
                var have = new HashSet<string>();
                var haveManagers = new HashSet<string>();
                List<BsonDocument> existingSchedules = Schedules().Find(new BsonDocument()).ToList();
                foreach (BsonDocument s in existingSchedules)
                {
                    if (Str(s, "titleId") != "") have.Add(Str(s, "titleId"));
                    if (Str(s, "projectManagerId") != "") haveManagers.Add(Str(s, "projectManagerId"));
                }

                List<BsonDocument> allSubsystem2Titles = Subsystem2Manuscripts.SchedulingTitles();
                var subsystem2TitleById = allSubsystem2Titles
                    .Where(t => DocId(t) != "")
                    .GroupBy(DocId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var subsystem2Managers = new HashSet<string>(
                    allSubsystem2Titles.Select(t => Str(t, "studentId")).Where(id => id != ""),
                    StringComparer.OrdinalIgnoreCase);
                List<BsonDocument> subsystem2Todo = allSubsystem2Titles
                    .Where(t => IsFirstDefenseRevision(t) || IsRedefenseRevision(t) && HasCompletedFirstDefense(Str(t, "studentId"), existingSchedules, subsystem2TitleById))
                    .Where(t => !have.Contains(DocId(t)))
                    .OrderBy(t => Stamp(t, "submittedAt") ?? DateTime.MinValue)
                    .ToList();

                List<BsonDocument> legacyTodo = Titles().Find(new BsonDocument()).ToList()
                    .Where(t => !have.Contains(DocId(t)) && !haveManagers.Contains(Str(t, "studentId")))
                    .Where(t => !subsystem2Managers.Contains(Str(t, "studentId")))
                    .OrderBy(t => Stamp(t, "submittedAt") ?? DateTime.MinValue)
                    .ToList();
                List<BsonDocument> todo = subsystem2Todo.Concat(legacyTodo).ToList();
                if (todo.Count == 0) return 0;

                EnsureIndex();
                PlanContext ctx = PlanContext.Load();
                int n = 0;
                foreach (BsonDocument t in todo)
                {
                    if (GenerateOne(ctx, t)) n++;
                }
                return n;
            }
        }

        /// <summary>Ensures the schedule for one exact Subsystem 2 manuscript version exists.</summary>
        public static bool EnsureGeneratedForSubsystem2Version(string subsystem2Id)
        {
            Guid requestedVersion;
            if (!Guid.TryParse(subsystem2Id, out requestedVersion)) return false;
            string normalizedVersion = requestedVersion.ToString("D");

            lock (Gate)
            {
                List<BsonDocument> titles = Subsystem2Manuscripts.SchedulingTitles();
                BsonDocument title = titles.FirstOrDefault(item =>
                    String.Equals(Str(item, "subsystem2Id"), normalizedVersion, StringComparison.OrdinalIgnoreCase));
                if (title == null) return false;
                if (!IsFirstDefenseRevision(title) && !IsRedefenseRevision(title)) return false;

                string titleId = DocId(title);
                var titleFilter = Builders<BsonDocument>.Filter.Eq("titleId", titleId);
                if (Schedules().Find(titleFilter).FirstOrDefault() != null) return true;

                var titlesById = titles
                    .Where(item => DocId(item) != "")
                    .GroupBy(DocId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                List<BsonDocument> existingSchedules = Schedules().Find(new BsonDocument()).ToList();
                if (IsRedefenseRevision(title) &&
                    !HasCompletedFirstDefense(Str(title, "studentId"), existingSchedules, titlesById))
                    return false;

                EnsureIndex();
                GenerateOne(PlanContext.Load(), title);
                return Schedules().Find(titleFilter).FirstOrDefault() != null;
            }
        }

        private static int RevisionOf(BsonDocument title)
        {
            int revision;
            string value = AnyStr(title, "versionNumber");
            if (String.IsNullOrWhiteSpace(value)) value = AnyStr(title, "revisions");
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out revision)
                ? revision
                : 1;
        }

        private static bool IsFirstDefenseRevision(BsonDocument title) { return RevisionOf(title) == 1; }
        private static bool IsRedefenseRevision(BsonDocument title) { return RevisionOf(title) == 2; }

        private static bool HasCompletedFirstDefense(string managerId, List<BsonDocument> schedules, Dictionary<string, BsonDocument> titlesById)
        {
            if (String.IsNullOrWhiteSpace(managerId)) return false;
            DateTime now = NowManila();
            foreach (BsonDocument schedule in schedules.Where(s => Str(s, "projectManagerId") == managerId))
            {
                int revision = 0;
                int.TryParse(AnyStr(schedule, "revisionNumber"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision);
                if (revision == 0)
                {
                    BsonDocument source;
                    if (titlesById.TryGetValue(Str(schedule, "titleId"), out source)) revision = RevisionOf(source);
                }
                if (revision != 1) continue;

                string status = CoDb.StatusOf(schedule);
                if (status == "completed" || status == "complete") return true;
                if (status != "approved") continue;
                DateTime? day = CoDb.ParseDay(Str(schedule, "date"));
                int? end = Minutes(schedule, "end");
                if (day.HasValue && end.HasValue && day.Value.AddMinutes(end.Value) <= now) return true;
            }
            return false;
        }

        /// <summary>Called right after a student submits a title.</summary>
        public static bool GenerateForTitle(string titleId)
        {
            lock (Gate)
            {
                ObjectId? oid = ParseId(titleId);
                if (!oid.HasValue) return false;
                BsonDocument t = Titles().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault();
                if (t == null) return false;
                if (Schedules().Find(Builders<BsonDocument>.Filter.Eq("titleId", titleId)).FirstOrDefault() != null) return false;

                EnsureIndex();
                return GenerateOne(PlanContext.Load(), t);
            }
        }

        /// <summary>Refreshes an editable generated schedule from its latest Subsystem 2 PDF.</summary>
        public static void RefreshPdfPanelMatching(string scheduleId)
        {
            lock (Gate)
            {
                ObjectId? oid = ParseId(scheduleId);
                if (!oid.HasValue) return;
                var filter = Builders<BsonDocument>.Filter.Eq("_id", oid.Value);
                BsonDocument sched = Schedules().Find(filter).FirstOrDefault();
                if (sched == null || !CoDb.IsEditable(sched)) return;

                BsonDocument title = Subsystem2Manuscripts.ForScheduleSubmission(Str(sched, "titleId"), includeRevisionCase: false) ?? TitleOf(sched);
                if (title == null || !title.Contains("sourceSubsystem2") || !title["sourceSubsystem2"].IsBoolean || !title["sourceSubsystem2"].AsBoolean) return;
                string savedKeywordSource = Str(sched, "keywordSource");
                if ((savedKeywordSource == "pdf" || savedKeywordSource == "gemini-pdf" || savedKeywordSource == "gemini-generated") && KeywordParserVersion(sched) >= PdfKeywordParserVersion) return;
                DateTime? lastAttempt = Stamp(sched, "keywordExtractionAttemptedAt");
                bool parserNeedsUpgrade = (savedKeywordSource == "pdf" || savedKeywordSource == "gemini-pdf" || savedKeywordSource == "gemini-generated") && KeywordParserVersion(sched) < PdfKeywordParserVersion;
                if (!parserNeedsUpgrade && lastAttempt.HasValue && DateTime.UtcNow - lastAttempt.Value < TimeSpan.FromHours(1)) return;

                string extractedSource;
                List<string> pdfKeywords = Subsystem2Manuscripts.ExtractPdfKeywords(title, out extractedSource);
                DateTime now = DateTime.UtcNow;
                if (pdfKeywords.Count == 0)
                {
                    List<string> fallbackKeywords = CoDb.Keywords(title);
                    PlanContext fallbackContext = PlanContext.Load();
                    var fallbackPanel = new List<BsonDocument>();
                    BsonValue previousPanel;
                    if (sched.TryGetValue("panelists", out previousPanel) && previousPanel != null && previousPanel.IsBsonArray)
                    {
                        foreach (BsonValue item in previousPanel.AsBsonArray)
                        {
                            if (!item.IsBsonDocument) continue;
                            BsonDocument user;
                            if (fallbackContext.Panelists.TryGetValue(Str(item.AsBsonDocument, "userId"), out user) &&
                                IsEligiblePanelist(user, Str(sched, "adviserId"))) fallbackPanel.Add(user);
                        }
                    }
                    var fallbackUpdate = Builders<BsonDocument>.Update
                        .Set("keywords", new BsonArray(fallbackKeywords))
                        .Set("keywordExtractionAttemptedAt", now)
                        .Set("keywordSource", fallbackKeywords.Count > 0 ? "submission-metadata" : "pdf-unavailable")
                        .Set("updatedAt", now);
                    if (fallbackPanel.Count == CoDb.PanelSize)
                        fallbackUpdate = fallbackUpdate.Set("panelists", PanelDocs(fallbackPanel, title, Str(sched, "adviserId"), fallbackKeywords));
                    Schedules().UpdateOne(filter, fallbackUpdate);
                    return;
                }

                PlanContext ctx = PlanContext.Load();
                string adviserId = Str(sched, "adviserId");
                string section = Str(sched, "section");
                EngProblem p = ProblemFor(title, adviserId, section, ctx);
                p.Keywords = pdfKeywords;

                DateTime? day = CoDb.ParseDay(Str(sched, "date"));
                int? start = Minutes(sched, "start"), end = Minutes(sched, "end");
                string roomId = Str(sched, "roomId");
                EngRoom room = ctx.Rooms.FirstOrDefault(r => r.Id == roomId);
                List<BsonDocument> panel = new List<BsonDocument>();
                if (day.HasValue && start.HasValue && end.HasValue && end.Value > start.Value && room != null)
                {
                    p.Days = new List<DateTime> { day.Value };
                    p.SlotStarts = new[] { start.Value };
                    p.Duration = end.Value - start.Value;
                    p.Rooms = new List<EngRoom> { room };
                    p.PersonBusy = (id, d, s, e) => ctx.PersonBooking(id, d, s, e, scheduleId) != null || ctx.ClassClash(id, d, s, e) != null;
                    p.SectionBusy = (targetSection, d, s, e) => ctx.SectionClassClash(targetSection, d, s, e);
                    p.RoomBusy = (id, d, s, e) => ctx.RoomBusy(id, d, s, e, scheduleId);
                    p.DayLoad = (id, d) => ctx.Bookings.Count(b => b.ScheduleId != scheduleId && b.Day == d && b.Panel.Contains(id));
                    p.TotalLoad = id => ctx.Bookings.Count(b => b.ScheduleId != scheduleId && b.Day >= ctx.Today && b.Panel.Contains(id));

                    EngResult result = ScheduleEngine.Solve(p);
                    if (result.Success)
                        panel = result.Panel.Select(pick => ctx.Panelists[pick.Faculty.Id]).ToList();
                }

                if (panel.Count == 0)
                {
                    BsonValue savedPanel;
                    if (sched.TryGetValue("panelists", out savedPanel) && savedPanel != null && savedPanel.IsBsonArray)
                    {
                        foreach (BsonValue item in savedPanel.AsBsonArray)
                        {
                            if (!item.IsBsonDocument) continue;
                            string id = Str(item.AsBsonDocument, "userId");
                            BsonDocument user;
                            if (ctx.Panelists.TryGetValue(id, out user)) panel.Add(user);
                        }
                    }
                }

                var update = Builders<BsonDocument>.Update
                    .Set("keywords", new BsonArray(pdfKeywords))
                    .Set("keywordSource", extractedSource)
                    .Set("keywordParserVersion", PdfKeywordParserVersion)
                    .Set("keywordExtractionAttemptedAt", now)
                    .Set("updatedAt", now);
                if (panel.Count == CoDb.PanelSize)
                    update = update.Set("panelists", PanelDocs(panel, title, adviserId, pdfKeywords));
                Schedules().UpdateOne(filter, update);
            }
        }

        private static EngProblem ProblemFor(BsonDocument title, string adviserId, string section, PlanContext ctx)
        {
            var p = new EngProblem
            {
                Title = Str(title, "title"),
                Abstract = Str(title, "abstract"),
                Keywords = CoDb.Keywords(title),
                Section = section ?? "",
                AdviserId = adviserId == "" ? null : adviserId,
                PanelSize = CoDb.PanelSize
            };
            foreach (var kv in ctx.Panelists)
            {
                p.Faculty.Add(new EngFaculty { Id = kv.Key, Name = PlainName(kv.Value), Tags = CoDb.Expertise(kv.Value) });
            }
            return p;
        }

        private static bool GenerateOne(PlanContext ctx, BsonDocument title)
        {
            string titleId = DocId(title);
            try
            {
                CoDb.Claims().InsertOne(new BsonDocument { { "titleId", titleId }, { "at", DateTime.UtcNow } });
            }
            catch (MongoWriteException)
            {
                return false;      // someone else is (or was) scheduling it
            }

            try
            {
                BsonDocument pm = ManagerOf(title);
                string adviserId = Str(title, "adviserId");
                if (adviserId == "" && pm != null) adviserId = Str(pm, "adviserId");
                string section = Str(title, "section") != "" ? Str(title, "section") : (pm == null ? "" : Str(pm, "section"));

                EngProblem p = ProblemFor(title, adviserId, section, ctx);
                // Keep the list page responsive: Cloudinary PDF extraction is done when
                // the coordinator opens the schedule, not while bulk schedules are generated.
                string keywordSource = p.Keywords.Count > 0 ? "submission-metadata" : "none";
                p.Rooms = ctx.Rooms;

                DateTime day = ctx.Today.AddDays(1);
                while (p.Days.Count < 20)
                {
                    if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday) p.Days.Add(day);
                    day = day.AddDays(1);
                }

                p.PersonBusy = (id, d, s, e) => ctx.PersonBooking(id, d, s, e, null) != null || ctx.ClassClash(id, d, s, e) != null;
                p.SectionBusy = (targetSection, d, s, e) => ctx.SectionClassClash(targetSection, d, s, e);
                p.RoomBusy = (id, d, s, e) => ctx.RoomBusy(id, d, s, e, null);
                p.DayLoad = (id, d) => ctx.DayLoad(id, d);
                p.TotalLoad = id => ctx.TotalLoad(id);

                EngResult r = ScheduleEngine.Solve(p);

                string program = CoDb.ProgramCode(pm != null && Str(pm, "program") != "" ? Str(pm, "program") : Str(title, "program"));
                string group = pm == null ? "" : FirstNonEmpty(AnyStr(pm, "group"), AnyStr(pm, "groupNo"), AnyStr(pm, "groupNumber"));
                DateTime now = DateTime.UtcNow;

                var doc = new BsonDocument
                {
                    { "projectManagerId", Str(title, "studentId") },
                    { "titleId", titleId },
                    { "revisionNumber", RevisionOf(title) },
                    { "title", Str(title, "title") },
                    { "authors", new BsonArray(SplitAuthors(Str(title, "authors"))) },
                    { "manager", pm == null ? Str(title, "projectManager") : PlainName(pm) },
                    { "program", program },
                    { "section", section },
                    { "group", group },
                    { "adviserId", adviserId },
                    { "adviser", Str(title, "adviser") },
                    { "keywords", new BsonArray(p.Keywords) },
                    { "keywordSource", keywordSource },
                    { "keywordParserVersion", 0 },
                    { "createdAt", now },
                    { "updatedAt", now }
                };

                if (r.Success)
                {
                    var panel = new BsonArray();
                    var ids = new HashSet<string>();
                    foreach (EngPick pick in r.Panel)
                    {
                        BsonDocument u = ctx.Panelists[pick.Faculty.Id];
                        if (!IsEligiblePanelist(u, adviserId)) continue;
                        panel.Add(new BsonDocument
                        {
                            { "userId", pick.Faculty.Id },
                            { "name", PlainName(u) },
                            { "role", pick.Role },
                            { "photo", Str(u, "profilePhoto") },
                            { "match", pick.Match },
                            { "percent", pick.Percent }
                        });
                        ids.Add(pick.Faculty.Id);
                    }

                    doc["date"] = CoDb.DayText(r.Date);
                    doc["start"] = r.Start;
                    doc["end"] = r.End;
                    doc["roomId"] = r.Room.Id;
                    doc["roomName"] = r.Room.Name;
                    doc["buildingName"] = r.Room.Building ?? "";
                    doc["panelists"] = panel;
                    doc["status"] = "generated";
                    doc["problem"] = "";
                    doc["generatedAt"] = now;
                    Schedules().InsertOne(doc);
                    SyncSubmission(doc);

                    var b = new Booking { ScheduleId = DocId(doc), RoomId = r.Room.Id, Day = r.Date, Start = r.Start, End = r.End };
                    foreach (string id in ids) { b.People.Add(id); b.Panel.Add(id); }
                    if (adviserId != "") b.People.Add(adviserId);
                    ctx.Bookings.Add(b);

                    CoDb.Log("Panel and schedule generated for \"" + Str(title, "title") + "\" (" + CoDb.SectionLabel(section, program) + ")");
                }
                else
                {
                    doc["panelists"] = new BsonArray();
                    doc["status"] = "under-review";
                    doc["problem"] = r.ConflictType;
                    doc["generatedAt"] = now;
                    Schedules().InsertOne(doc);
                    SyncSubmission(doc);

                    CoDb.Conflicts().InsertOne(new BsonDocument
                    {
                        { "scheduleId", DocId(doc) },
                        { "titleId", titleId },
                        { "type", r.ConflictType },
                        { "description", r.ConflictDescription },
                        { "steps", new BsonArray(r.Steps.Select(s => new BsonDocument { { "title", s.Title }, { "text", s.Text } })) },
                        { "status", "in-progress" },
                        { "detectedAt", now },
                        { "resolvedAt", BsonNull.Value }
                    });

                    CoDb.Log("Conflict detected for \"" + Str(title, "title") + "\": " + r.ConflictType);
                }
                return true;
            }
            catch
            {
                // let the next page load try again
                try { CoDb.Claims().DeleteOne(Builders<BsonDocument>.Filter.Eq("titleId", titleId)); } catch { }
                throw;
            }
        }

        // Shows the schedule's status (To Submit / Under Review / Submitted) on the capstone_submissions document.
        private static void SyncSubmission(BsonDocument sched)
        {
            try { Subsystem2Manuscripts.MarkSubmission(Str(sched, "titleId"), DocId(sched), CoDb.CoStatus(sched), Str(sched, "problem")); }
            catch { /* the schedule is already saved; CoordinatorData.Load repeats this on the next visit */ }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
        }

        // ------------------------------------------------------------------
        // Editing / submitting
        // ------------------------------------------------------------------

        /// <summary>Checks a proposed panel + date + time + room. Error = blocked; Soft = needs a confirmation.</summary>
        public static PlanCheck Check(BsonDocument sched, PlanInput input, PlanContext ctx)
        {
            var res = new PlanCheck();
            string schedId = DocId(sched);
            string adviserId = Str(sched, "adviserId");

            // panelists
            List<string> names = (input.Panelists ?? new List<string>()).Select(n => (n ?? "").Trim()).ToList();
            if (names.Count != CoDb.PanelSize || names.Any(n => n == ""))
            {
                res.Error = "Please choose " + CoDb.PanelSize + " panelists.";
                return res;
            }
            var seen = new HashSet<string>();
            foreach (string n in names)
            {
                string id = ctx.IdByName(n);
                if (id == "" || !ctx.Panelists.ContainsKey(id))
                {
                    res.Error = "\"" + n + "\" is not an active panelist. Pick a name from the list.";
                    return res;
                }
                if (!IsEligiblePanelist(ctx.Panelists[id], adviserId))
                {
                    res.Error = n + " must be an active professor/panelist and cannot be the group's adviser.";
                    return res;
                }
                if (!seen.Add(id)) { res.Error = "A panelist can only be assigned once."; return res; }
                res.Panel.Add(ctx.Panelists[id]);
            }

            // date and time
            DateTime? day = CoDb.ParseDay(input.Date);
            if (!day.HasValue) { res.Error = "Please choose a defense date."; return res; }
            if (day.Value < ctx.Today) { res.Error = "The defense date cannot be in the past."; return res; }
            int? start = CoDb.ParseHhmm(input.Start), end = CoDb.ParseHhmm(input.End);
            if (!start.HasValue || !end.HasValue) { res.Error = "Please set a start and end time."; return res; }
            if (end.Value <= start.Value) { res.Error = "End time must be later than start time."; return res; }
            if (end.Value - start.Value < 30) { res.Error = "A defense must be at least 30 minutes long."; return res; }
            if (start.Value < CoDb.FirstMinute || end.Value > CoDb.LastMinute)
            {
                res.Error = "Defenses are held between " + CoDb.FmtMinutes(CoDb.FirstMinute) + " and " + CoDb.FmtMinutes(CoDb.LastMinute) + ".";
                return res;
            }
            res.Day = day.Value;
            res.Start = start.Value;
            res.End = end.Value;

            string section = Str(sched, "section");
            if (section != "" && ctx.SectionClassClash(section, res.Day, res.Start, res.End))
            {
                res.Error = "The " + section + " section has a scheduled class during that defense time. Choose a free section period.";
                return res;
            }

            // room
            string roomName = (input.Room ?? "").Trim();
            EngRoom room = ctx.Rooms.FirstOrDefault(r => string.Equals(r.Name, roomName, StringComparison.OrdinalIgnoreCase));
            if (room == null) { res.Error = "Room \"" + roomName + "\" was not found or is unavailable."; return res; }
            res.Room = room;

            if (ctx.RoomBusy(room.Id, res.Day, res.Start, res.End, schedId))
            {
                res.Error = "Room " + room.Name + " is already booked at that time.";
                return res;
            }

            // people
            var people = new List<string>();
            foreach (BsonDocument u in res.Panel) people.Add(DocId(u));
            if (adviserId != "") people.Add(adviserId);

            foreach (string id in people)
            {
                string who = id == adviserId ? "The adviser (" + ctx.NameOf(id) + ")" : ctx.NameOf(id);
                Booking other = ctx.PersonBooking(id, res.Day, res.Start, res.End, schedId);
                if (other != null)
                {
                    res.Error = who + " is already in another defense from " + CoDb.FmtMinutes(other.Start) + " to " + CoDb.FmtMinutes(other.End) + " that day.";
                    return res;
                }
                BsonDocument cls = ctx.ClassClash(id, res.Day, res.Start, res.End);
                if (cls != null)
                {
                    res.Soft.Add(who + " has class " + Str(cls, "subjectCode") + " (" + CoDb.FmtMinutes(Minutes(cls, "start") ?? 0) + " - "
                        + CoDb.FmtMinutes(Minutes(cls, "end") ?? 0) + ") on " + res.Day.DayOfWeek + ".");
                }
            }
            return res;
        }

        private static BsonArray PanelDocs(List<BsonDocument> panel, BsonDocument title, string adviserId, List<string> keywords)
        {
            var p = new EngProblem { Title = Str(title, "title"), Abstract = Str(title, "abstract"), Keywords = keywords ?? CoDb.Keywords(title) };
            var arr = new BsonArray();

            // same rule as the generator: highest percentage = Chairman, the rest = Members
            var picks = new List<EngPick>();
            var users = new Dictionary<string, BsonDocument>();
            foreach (BsonDocument u in panel)
            {
                if (!IsEligiblePanelist(u, adviserId)) continue;
                var f = new EngFaculty { Id = DocId(u), Name = PlainName(u), Tags = CoDb.Expertise(u) };
                int pts = ScheduleEngine.Points(f, p);
                picks.Add(new EngPick { Faculty = f, Points = pts, Percent = ScheduleEngine.Percent(f, p), Match = ScheduleEngine.Level(pts) });
                users[f.Id] = u;
            }

            foreach (EngPick pick in ScheduleEngine.AssignRoles(picks, id => 0))
            {
                BsonDocument u = users[pick.Faculty.Id];
                arr.Add(new BsonDocument
                {
                    { "userId", pick.Faculty.Id },
                    { "name", PlainName(u) },
                    { "role", pick.Role },
                    { "photo", Str(u, "profilePhoto") },
                    { "match", pick.Match },
                    { "percent", pick.Percent }
                });
            }
            return arr;
        }

        private static BsonDocument TitleOf(BsonDocument sched)
        {
            ObjectId? oid = ParseId(Str(sched, "titleId"));
            return oid.HasValue ? Titles().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
        }

        /// <summary>The coordinator edited the panelists, date, time or room. Returns an error, or null (Soft is filled when confirmation is needed).</summary>
        public static string Save(string scheduleId, PlanInput input, bool force, List<string> soft)
        {
            lock (Gate)
            {
                ObjectId? oid = ParseId(scheduleId);
                BsonDocument sched = oid.HasValue ? Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
                if (sched == null) return "Schedule not found.";
                if (!CoDb.IsEditable(sched)) return "This schedule was already submitted and can no longer be edited.";

                PlanCheck chk = Check(sched, input, PlanContext.Load());
                if (chk.Error != null) return chk.Error;
                if (chk.Soft.Count > 0 && !force) { soft.AddRange(chk.Soft); return null; }

                BsonDocument title = Subsystem2Manuscripts.ForScheduleSubmission(Str(sched, "titleId"), includeRevisionCase: false) ?? TitleOf(sched);
                List<string> matchingKeywords = CoDb.Keywords(sched);
                string keywordSource = Str(sched, "keywordSource");
                if (title != null && title.Contains("sourceSubsystem2") && title["sourceSubsystem2"].IsBoolean && title["sourceSubsystem2"].AsBoolean &&
                    ((keywordSource != "pdf" && keywordSource != "gemini-pdf" && keywordSource != "gemini-generated") || KeywordParserVersion(sched) < PdfKeywordParserVersion))
                {
                    string extractedSource;
                    List<string> extracted = Subsystem2Manuscripts.ExtractPdfKeywords(title, out extractedSource);
                    if (extracted.Count > 0)
                    {
                        matchingKeywords = extracted;
                        keywordSource = extractedSource;
                    }
                }
                if (matchingKeywords.Count == 0 && title != null) matchingKeywords = CoDb.Keywords(title);
                var u = Builders<BsonDocument>.Update;
                Schedules().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid.Value),
                    u.Combine(
                        u.Set("date", CoDb.DayText(chk.Day)),
                        u.Set("start", chk.Start),
                        u.Set("end", chk.End),
                        u.Set("roomId", chk.Room.Id),
                        u.Set("roomName", chk.Room.Name),
                        u.Set("buildingName", chk.Room.Building ?? ""),
                        u.Set("panelists", PanelDocs(chk.Panel, title ?? new BsonDocument(), Str(sched, "adviserId"), matchingKeywords)),
                        u.Set("keywords", new BsonArray(matchingKeywords)),
                        u.Set("keywordSource", keywordSource != "" ? keywordSource : matchingKeywords.Count > 0 ? "submission-metadata" : "none"),
                        u.Set("keywordParserVersion", (keywordSource == "pdf" || keywordSource == "gemini-pdf" || keywordSource == "gemini-generated") ? PdfKeywordParserVersion : 0),
                        u.Set("keywordExtractionAttemptedAt", DateTime.UtcNow),
                        u.Set("updatedAt", DateTime.UtcNow)));

                CoDb.Log("Schedule edited for \"" + Str(sched, "title") + "\"");
                return null;
            }
        }

        /// <summary>Submit to the Dean. Returns an error message, or null.</summary>
        public static string Submit(string scheduleId, string submittedBy = null)
        {
            lock (Gate)
            {
                ObjectId? oid = ParseId(scheduleId);
                BsonDocument sched = oid.HasValue ? Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault() : null;
                if (sched == null) return "Schedule not found.";
                if (!CoDb.IsEditable(sched)) return "This schedule was already submitted.";

                var names = new List<string>();
                BsonValue pv;
                if (sched.TryGetValue("panelists", out pv) && pv != null && pv.IsBsonArray)
                {
                    foreach (BsonValue p in pv.AsBsonArray) if (p.IsBsonDocument) names.Add(Str(p.AsBsonDocument, "name"));
                }
                if (names.Count != CoDb.PanelSize || Str(sched, "date") == "" || Str(sched, "roomId") == "")
                {
                    return "Choose the panelists, date, time and room first (use the pencil), then submit.";
                }

                var input = new PlanInput
                {
                    Panelists = names,
                    Date = Str(sched, "date"),
                    Start = CoDb.Hhmm(Minutes(sched, "start") ?? 0),
                    End = CoDb.Hhmm(Minutes(sched, "end") ?? 0),
                    Room = Str(sched, "roomName")
                };
                PlanCheck chk = Check(sched, input, PlanContext.Load());
                if (chk.Error != null) return chk.Error;

                DateTime now = DateTime.UtcNow;
                var u = Builders<BsonDocument>.Update;
                Schedules().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid.Value),
                    u.Combine(u.Set("status", "submitted"), u.Set("problem", ""), u.Set("submittedAt", now), u.Set("submittedBy", submittedBy ?? ""), u.Set("updatedAt", now)));

                var cf = Builders<BsonDocument>.Filter;
                CoDb.Conflicts().UpdateMany(
                    cf.And(cf.Eq("scheduleId", scheduleId), cf.Eq("status", "in-progress")),
                    Builders<BsonDocument>.Update.Combine(
                        Builders<BsonDocument>.Update.Set("status", "resolved"),
                        Builders<BsonDocument>.Update.Set("resolvedAt", now)));

                sched["status"] = "submitted";
                sched["problem"] = "";
                SyncSubmission(sched);

                CoDb.Log("Schedule submitted to the Dean for \"" + Str(sched, "title") + "\"");
                return null;
            }
        }
    }
}
