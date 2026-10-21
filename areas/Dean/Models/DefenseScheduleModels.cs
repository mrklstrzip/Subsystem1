using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Dean.Models
{
    public class DefenseScheduleItem
    {
        public int Id { get; set; }
        public string Manuscript { get; set; }
        public string Section { get; set; }      // e.g. "BSIT-4A"
        public string Author { get; set; }
        public string Adviser { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Room { get; set; }
        public List<string> Panelists { get; set; } = new List<string>();
        public string Status { get; set; }       // Completed | Ongoing | Upcoming

        public string Program => Section?.Split('-')[0] ?? "";
        public string TimeRange =>
            DateTime.Today.Add(StartTime).ToString("h:mmtt") + " - " +
            DateTime.Today.Add(EndTime).ToString("h:mmtt");
        public string StatusClass => (Status ?? "").ToLower();
        public string ProgramClass => Program.ToLower();
    }
    public class DefenseScheduleViewModel
    {
        public List<DefenseScheduleItem> Items { get; set; } = new List<DefenseScheduleItem>();

        // current filter values
        public string Search { get; set; }
        public string Program { get; set; } = "All";
        public string Status { get; set; } = "All";
        public string Adviser { get; set; } = "All";

        // dropdown options
        public List<string> Programs { get; set; } = new List<string>();
        public List<string> Statuses { get; set; } = new List<string> { "Completed", "Ongoing", "Upcoming" };
        public List<string> Advisers { get; set; } = new List<string>();
    }
}