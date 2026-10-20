using System;
using System.Collections.Generic;
using System.Linq;
using Subsystem1.Areas.Student.ViewModels;

namespace Subsystem1.Areas.Student.Models
{
    /// <summary>
    /// Demo in-memory store shared by every visitor; it resets when the app restarts.
    /// Replace with your database / Entity Framework code when it exists.
    /// </summary>
    public class SubmissionStore
    {
        public static readonly SubmissionStore Instance = new SubmissionStore();

        private int _nextId = 1;
        public List<ManuscriptItemDto> Titles { get; } = new List<ManuscriptItemDto>();
        public List<LogItem> Log { get; } = new List<LogItem>();

        // progress flags (normally set by the coordinator / panel side)
        public bool TitleSubmitted { get; private set; }
        public DateTime? DefenseDate { get; private set; }
        public int PanelistCount { get; private set; }
        public string Room { get; private set; }
        public bool DefenseScheduled { get; private set; }
        public bool DefenseComplete { get; private set; }

        /// <summary>When the tracker last changed (newest log entry), or null if nothing has happened yet.</summary>
        public DateTime? LastChanged
        {
            get { return Log.Count == 0 ? (DateTime?)null : Log.Max(l => l.Timestamp); }
        }

        public class PanelistInfo
        {
            public string Name { get; set; }
            public string Role { get; set; }
            public string Department { get; set; }
            public string PhotoUrl { get; set; }   // optional, e.g. "~/Content/img/clove.jpg"
        }

        /// <summary>Demo pool of panelists. The first two are used when the "Panelist Assigned" step advances.</summary>
        public List<PanelistInfo> Panelists { get; } = new List<PanelistInfo>
        {
            new PanelistInfo { Name = "Ms. Clove Batumbakal", Role = "Panelist", Department = "College of Computer Studies" },
            new PanelistInfo { Name = "Dr. Momonga Fuentes", Role = "Adviser", Department = "College of Computer Studies" },
            new PanelistInfo { Name = "Mr. Gojo Montefalco", Role = "External Panelist", Department = "Industry Partner" },
            new PanelistInfo { Name = "Ms. Chiikawa Villareal", Role = "Panelist", Department = "College of Computer Studies" }
        };

        public static readonly string[] Programs =
            { "BS Computer Science", "BS Information Technology", "BS Information System" };
        public static readonly string[] Advisors =
            { "Dr. Momonga Fuentes", "Ms. Clove Batumbakal", "Ms. Chiikawa Villareal" };

        public ManuscriptItemDto Add(StudentTitleInput i)
        {
            var manager = string.IsNullOrWhiteSpace(i.ProjectManager) ? "Project Manager" : i.ProjectManager.Trim();
            var s = new ManuscriptItemDto
            {
                Id = _nextId++,
                Title = i.Title.Trim(),
                Abstract = i.Abstract.Trim(),
                Keywords = (i.Keywords ?? "").Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToList(),
                Authors = i.Authors,
                DatePublished = i.DatePublished,
                Program = i.Program,
                Adviser = i.Advisor,
                ProjectManager = manager,
                DateAndSubmissionTime = DateTime.Now,
                Status = "Submitted"
            };
            Titles.Insert(0, s);
            TitleSubmitted = true;
            Log.Insert(0, new LogItem { Step = 0, Timestamp = s.DateAndSubmissionTime, Text = "Capstone Title submitted by **" + manager + "**" });
            return s;
        }

        /// <summary>DEMO: completes the next pending step (Date Set, Panelist, Room, Defense Scheduled), in order.</summary>
        public bool AdvanceNext()
        {
            if (!TitleSubmitted) return false;   // step 1 needs a real title first

            if (!DefenseDate.HasValue)
            {
                DefenseDate = DateTime.Today.AddDays(25).AddHours(9);
                AddLog(1, "Date Set by **" + DefenseDate.Value.ToString("MMMM d, yyyy") + "** at **9:00AM**");
            }
            else if (PanelistCount == 0)
            {
                PanelistCount = Panelists.Count;
                AddLog(2, "**" + Panelists[0].Name + "** has been assigned as Panelist");
                if (Panelists.Count > 1) AddLog(2, "**" + Panelists[1].Name + "** was assigned as Adviser");
            }
            else if (Room == null)
            {
                Room = "IL504";
                AddLog(3, "Room set to **IL504**");
            }
            else if (!DefenseScheduled)
            {
                DefenseScheduled = true;
                AddLog(4, "Status changed to **\"Defense Scheduled\"**");
            }
            else return false;

            Touch();
            return true;
        }

        /// <summary>Adds an activity-log entry tagged with the tracker step (1-5) it belongs to.</summary>
        private void AddLog(int step, string text)
        {
            Log.Insert(0, new LogItem { Step = step, Timestamp = DateTime.Now, Text = text });
        }

        /// <summary>Keeps the table badge in step with the tracker.</summary>
        private void Touch()
        {
            if (DefenseComplete) SetStatusIfSingle("Completed");
            else if (DefenseDate.HasValue) SetStatusIfSingle("Processing");
            else SetStatusIfSingle("Submitted");
        }

        /// <summary>
        /// DEMO: undo the step at <paramref name="index"/> (0-based, as in BuildSteps) and every step after it,
        /// and remove their log entries. Step 0 (the submitted title) is never reverted.
        /// </summary>
        public void RevertTo(int index)
        {
            if (index < 1 || index > 5) return;

            if (index <= 5) DefenseComplete = false;
            if (index <= 4) DefenseScheduled = false;
            if (index <= 3) Room = null;
            if (index <= 2) PanelistCount = 0;
            if (index <= 1) DefenseDate = null;

            Log.RemoveAll(l => l.Step >= index);
            Touch();
        }

        // keeps the table badge in step with the demo, only when the student has submitted a single title
        private void SetStatusIfSingle(string status)
        {
            if (Titles.Count == 1) Titles[0].Status = status;
        }

        public void SetDefenseComplete()
        {
            DefenseComplete = true;
            AddLog(5, "Defense marked as **Complete**");
            Touch();
        }

        /// <summary>Sample data that mirrors the three design states: empty | inprogress | complete.</summary>
        public void LoadDemo(string demo)
        {
            Titles.Clear(); Log.Clear(); _nextId = 1;
            TitleSubmitted = false; DefenseDate = null; PanelistCount = 0; Room = null;
            DefenseScheduled = false; DefenseComplete = false;
            if (demo != "inprogress" && demo != "complete") return;

            int y = DateTime.Today.Year;
            const string title = "SmartQueue: An RFID-Based Queuing and Notification System for University Clinics";
            const string abs = "Lorem ipsum dolor sit amet, consectetur adipisci elit, sed eiusmod tempor incidunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur.";
            var rows = new[]
            {
                new { St = "Submitted", Min = 33 },
                new { St = "Processing", Min = 32 },
                new { St = "Completed", Min = 32 }
            };
            foreach (var r in rows)
            {
                Titles.Add(new ManuscriptItemDto
                {
                    Id = _nextId++,
                    Title = title,
                    Abstract = abs,
                    Keywords = new List<string> { "Lorem ipsum" },
                    Program = "BS Computer Science",
                    Adviser = "Dr. Momonga Fuentes",
                    ProjectManager = "Rizza Mae Diaz",
                    DateAndSubmissionTime = new DateTime(y, 9, 10, 14, r.Min, 0),
                    Status = r.St
                });
            }

            Log.Add(new LogItem { Step = 2, Timestamp = new DateTime(y, 9, 12, 10, 0, 0), Text = "**Mr. Gojo Montefalco** has been assigned as External Panelist" });
            Log.Add(new LogItem { Step = 2, Timestamp = new DateTime(y, 9, 11, 13, 0, 0), Text = "**Ms. Clove Batumbakal** has been assigned as Panelist" });
            Log.Add(new LogItem { Step = 1, Timestamp = new DateTime(y, 9, 10, 10, 0, 0), Text = "Date Set by **November 19, 2026** at **9:00AM**" });
            Log.Add(new LogItem { Step = 1, Timestamp = new DateTime(y, 9, 10, 13, 0, 0), Text = "Status changed from **\"Manuscript Submitted\"** to **\"Date Set\"**" });
            Log.Add(new LogItem { Step = 0, Timestamp = new DateTime(y, 9, 10, 13, 0, 0), Text = "Capstone Title submitted by **Rizza Mae Diaz**" });

            TitleSubmitted = true;
            DefenseDate = new DateTime(2026, 11, 19, 9, 0, 0);
            PanelistCount = 4;
            Room = "IL504";
            DefenseScheduled = true;
            DefenseComplete = demo == "complete";
        }

        public List<StepItem> BuildSteps()
        {
            return new List<StepItem>
            {
                new StepItem { Label = "Title Submitted", Detail = TitleSubmitted ? "Complete" : "Pending", Done = TitleSubmitted },
                new StepItem { Label = "Date Set", Detail = DefenseDate.HasValue ? DefenseDate.Value.ToString("MMM. d, yyyy - h:mmtt") : "Pending", Done = DefenseDate.HasValue },
                new StepItem { Label = "Panelist Assigned", Detail = PanelistCount > 0 ? PanelistCount + " Panelist Assigned" : "Pending", Done = PanelistCount > 0 },
                new StepItem { Label = "Room Set", Detail = Room ?? "Pending", Done = Room != null },
                new StepItem { Label = "Defense Scheduled", Detail = DefenseScheduled ? "Complete" : "Pending", Done = DefenseScheduled },
                new StepItem { Label = "Defense Complete", Detail = DefenseComplete ? "Complete" : "Pending", Done = DefenseComplete },
            };
        }
    }
}