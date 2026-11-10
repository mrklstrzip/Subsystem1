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
                c.Rooms.Add(new EngRoom { Id = DocId(r), Name = Str(r, "name") });
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
                if (v.StartsWith("IE ", StringComparison.OrdinalIgnoreCase) || v.StartsWith("IE-", StringComparison.OrdinalIgnoreCase)
                    || v.StartsWith("IE(", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
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
                foreach (BsonDocument s in Schedules().Find(new BsonDocument()).ToList())
                {
                    if (Str(s, "titleId") != "") have.Add(Str(s, "titleId"));
                    if (Str(s, "projectManagerId") != "") haveManagers.Add(Str(s, "projectManagerId"));
                }

                List<BsonDocument> todo = Titles().Find(new BsonDocument()).ToList()
                    .Where(t => !have.Contains(DocId(t)) && !haveManagers.Contains(Str(t, "studentId")))
                    .OrderBy(t => Stamp(t, "submittedAt") ?? DateTime.MinValue)
                    .ToList();
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

        private static EngProblem ProblemFor(BsonDocument title, string adviserId, PlanContext ctx)
        {
            var p = new EngProblem
            {
                Title = Str(title, "title"),
                Abstract = Str(title, "abstract"),
                Keywords = CoDb.Keywords(title),
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

                EngProblem p = ProblemFor(title, adviserId, ctx);
                p.Rooms = ctx.Rooms;

                DateTime day = ctx.Today.AddDays(1);
                while (p.Days.Count < 20)
                {
                    if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday) p.Days.Add(day);
                    day = day.AddDays(1);
                }

                p.PersonBusy = (id, d, s, e) => ctx.PersonBooking(id, d, s, e, null) != null || ctx.ClassClash(id, d, s, e) != null;
                p.RoomBusy = (id, d, s, e) => ctx.RoomBusy(id, d, s, e, null);
                p.DayLoad = (id, d) => ctx.DayLoad(id, d);
                p.TotalLoad = id => ctx.TotalLoad(id);

                EngResult r = ScheduleEngine.Solve(p);

                string program = CoDb.ProgramCode(pm != null && Str(pm, "program") != "" ? Str(pm, "program") : Str(title, "program"));
                string section = Str(title, "section") != "" ? Str(title, "section") : (pm == null ? "" : Str(pm, "section"));
                string group = pm == null ? "" : FirstNonEmpty(AnyStr(pm, "group"), AnyStr(pm, "groupNo"), AnyStr(pm, "groupNumber"));
                DateTime now = DateTime.UtcNow;

                var doc = new BsonDocument
                {
                    { "projectManagerId", Str(title, "studentId") },
                    { "titleId", titleId },
                    { "title", Str(title, "title") },
                    { "authors", new BsonArray(SplitAuthors(Str(title, "authors"))) },
                    { "manager", pm == null ? Str(title, "projectManager") : PlainName(pm) },
                    { "program", program },
                    { "section", section },
                    { "group", group },
                    { "adviserId", adviserId },
                    { "adviser", Str(title, "adviser") },
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
                    doc["panelists"] = panel;
                    doc["status"] = "generated";
                    doc["problem"] = "";
                    doc["generatedAt"] = now;
                    Schedules().InsertOne(doc);

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
                if (id == adviserId) { res.Error = n + " is the group's adviser and cannot also be a panelist."; return res; }
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

        private static BsonArray PanelDocs(List<BsonDocument> panel, BsonDocument title, string adviserId)
        {
            var p = new EngProblem { Title = Str(title, "title"), Abstract = Str(title, "abstract"), Keywords = CoDb.Keywords(title) };
            var arr = new BsonArray();

            // same rule as the generator: highest percentage = Chairman, the rest = Members
            var picks = new List<EngPick>();
            var users = new Dictionary<string, BsonDocument>();
            foreach (BsonDocument u in panel)
            {
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

                BsonDocument title = TitleOf(sched);
                var u = Builders<BsonDocument>.Update;
                Schedules().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid.Value),
                    u.Combine(
                        u.Set("date", CoDb.DayText(chk.Day)),
                        u.Set("start", chk.Start),
                        u.Set("end", chk.End),
                        u.Set("roomId", chk.Room.Id),
                        u.Set("roomName", chk.Room.Name),
                        u.Set("panelists", PanelDocs(chk.Panel, title ?? new BsonDocument(), Str(sched, "adviserId"))),
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

                CoDb.Log("Schedule submitted to the Dean for \"" + Str(sched, "title") + "\"");
                return null;
            }
        }
    }
}
