using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Dean.Models
{
    public class ScheduleListItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Section { get; set; }   // e.g. "BSCS-4A"
        public string Program { get; set; }   // e.g. "BSCS"
        public string Author { get; set; }
        public string Adviser { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }    // "To Approve" | "Approved"
    }

    public class PanelistVM
    {
        public string Name { get; set; }
        public string Expertise { get; set; } // High | Medium | Low
    }

    public class ScheduleDetailsVM
    {
        public int Id { get; set; }
        public string ProjectTitle { get; set; }
        public string DateSubmitted { get; set; }
        public string Status { get; set; }
        public string Student { get; set; }
        public string ProgramSection { get; set; }
        public string Adviser { get; set; }
        public string Abstract { get; set; }
        public string Keywords { get; set; }
        public List<PanelistVM> Panelists { get; set; }
        public string Time { get; set; }
        public string DefenseDate { get; set; }
        public string Room { get; set; }
    }
}