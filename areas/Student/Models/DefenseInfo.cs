using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.Student.Models
{
    public class PanelistInfo
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
        public string PhotoUrl { get; set; }   // "~/Content/images/x.jpg", a file name in Content/images, or empty
    }

    /// <summary>
    /// One defense_schedules document, read the same way for every role that shows it
    /// (the Student pages and the Adviser pages). The layout is described in StudentDb.cs.
    /// </summary>
    public class DefenseInfo
    {
        public string ScheduleId { get; private set; }
        public DateTime? Start { get; private set; }      // date + start time (Philippine time)
        public DateTime? End { get; private set; }        // date + end time (end of day when none was saved)
        public string RoomName { get; private set; }      // "IL504" or null
        public string Room { get; private set; }          // "Room IL504" / "Auditorium" or null
        public List<PanelistInfo> Panelists { get; private set; }
        public bool CompletedFlag { get; private set; }   // the schedule itself says "completed"
        public DateTime? Stamp { get; private set; }      // approvedAt, else updatedAt, else createdAt

        public bool IsScheduled
        {
            get { return Start.HasValue && Room != null && Panelists.Count > 0; }
        }

        private DefenseInfo()
        {
            Panelists = new List<PanelistInfo>();
        }

        /// <summary>Only schedules the Dean approved (or that are finished) are shown to students and advisers.</summary>
        public static bool IsVisibleStatus(BsonDocument d)
        {
            string st = Str(d, "status").Trim().ToLowerInvariant();
            return st == "approved" || st == "completed" || st == "complete";
        }

        /// <summary>"IL504" -> "Room IL504"; a named place such as "Auditorium" stays as is.</summary>
        public static string RoomDisplay(string name)
        {
            name = (name ?? "").Trim();
            if (name == "") return null;
            return name.Any(char.IsDigit) ? "Room " + name : name;
        }

        public static string PhotoPath(string photo)
        {
            photo = (photo ?? "").Trim();
            if (photo == "") return null;
            if (photo.StartsWith("~/") || photo.StartsWith("/") || photo.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return photo;
            return "~/Content/images/" + photo;
        }

        /// <param name="roomNameById">Gives the saved name of a room from its id (or null / empty).</param>
        public static DefenseInfo Parse(BsonDocument sched, Func<string, string> roomNameById)
        {
            var info = new DefenseInfo { ScheduleId = DocId(sched) };

            DateTime day;
            if (DateTime.TryParseExact(Str(sched, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out day))
            {
                int? start = Minutes(sched, "start");
                int? end = Minutes(sched, "end");
                info.Start = day.AddMinutes(start ?? 0);
                info.End = end.HasValue ? day.AddMinutes(end.Value) : day.AddDays(1);
            }

            BsonValue pv;
            if (sched.TryGetValue("panelists", out pv) && pv != null && pv.IsBsonArray)
            {
                foreach (BsonValue p in pv.AsBsonArray)
                {
                    if (p.IsString && p.AsString.Trim() != "")
                    {
                        info.Panelists.Add(new PanelistInfo { Name = p.AsString.Trim(), Role = "Panelist" });
                    }
                    else if (p.IsBsonDocument && Str(p.AsBsonDocument, "name").Trim() != "")
                    {
                        BsonDocument pd = p.AsBsonDocument;
                        info.Panelists.Add(new PanelistInfo
                        {
                            Name = Str(pd, "name").Trim(),
                            Role = Str(pd, "role") != "" ? Str(pd, "role") : "Panelist",
                            Department = Str(pd, "department"),
                            PhotoUrl = PhotoPath(Str(pd, "photo"))
                        });
                    }
                }
            }

            string name = "";
            string rid = AnyStr(sched, "roomId");
            if (rid != "" && roomNameById != null) name = roomNameById(rid) ?? "";
            if (name == "") name = Str(sched, "roomName");
            if (name == "") name = Str(sched, "room");
            name = name.Trim();
            if (name != "")
            {
                info.RoomName = name;
                info.Room = RoomDisplay(name);
            }

            string st = Str(sched, "status").Trim().ToLowerInvariant();
            BsonValue cv;
            info.CompletedFlag = st == "completed" || st == "complete"
                || (sched.TryGetValue("completed", out cv) && cv != null && cv.IsBoolean && cv.AsBoolean);

            info.Stamp = Stamp(sched, "approvedAt") ?? Stamp(sched, "updatedAt") ?? Stamp(sched, "createdAt");
            return info;
        }
    }
}
