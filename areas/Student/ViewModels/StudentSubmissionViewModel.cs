using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Student.ViewModels
{
    public class ManuscriptItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime DateAndSubmissionTime { get; set; }
        public string Status { get; set; }            // Submitted | Processing | Completed

        // new: shown in the "View Capstone Title" pop-up
        public string Abstract { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string Authors { get; set; }
        public DateTime? DatePublished { get; set; }
        public string Program { get; set; }
        public string Adviser { get; set; }
        public string ProjectManager { get; set; }

        // "SmartQueue: An RFID-Based ..." -> bold "SmartQueue" + ": An RFID-Based ..."
        public string TitleBold
        {
            get { var i = (Title ?? "").IndexOf(':'); return i >= 0 ? Title.Substring(0, i) : Title; }
        }
        public string TitleRest
        {
            get { var i = (Title ?? "").IndexOf(':'); return i >= 0 ? Title.Substring(i) : ""; }
        }
    }

    public class StepItem
    {
        public string Label { get; set; }
        public string Detail { get; set; }
        public bool Done { get; set; }
    }

    public class LogItem
    {
        public DateTime Timestamp { get; set; }
        /// <summary>Tracker step this entry belongs to (0 = title ... 5 = defense complete); used when a step is reverted.</summary>
        public int Step { get; set; }
        /// <summary>Wrap text in **double asterisks** to show it in bold.</summary>
        public string Text { get; set; }
    }

    /// <summary>Posted by the "Add Capstone Title" pop-up.</summary>
    public class StudentTitleInput
    {
        [Required] public string Title { get; set; }
        [Required] public string Abstract { get; set; }
        public string Keywords { get; set; }            // comma separated
        [Required] public string Authors { get; set; }
        public DateTime? DatePublished { get; set; }
        [Required] public string Program { get; set; }
        [Required] public string Advisor { get; set; }
        public string ProjectManager { get; set; }      // filled by JS from the logged-in user
    }

    public class StudentSubmissionViewModel
    {
        // Table
        public List<ManuscriptItemDto> Submissions { get; set; } = new List<ManuscriptItemDto>();

        // Progress tracker + activity log (new)
        public List<StepItem> Steps { get; set; } = new List<StepItem>();
        public List<LogItem> Log { get; set; } = new List<LogItem>();
        public List<string> Programs { get; set; } = new List<string>();
        public List<string> Advisors { get; set; } = new List<string>();

        public bool HasTitle { get { return Submissions.Count > 0; } }
        public bool AllComplete { get { return Steps.Count > 0 && Steps.All(s => s.Done); } }
        /// <summary>Index of the first step that is not done yet (-1 when everything is complete).</summary>
        public int NextStepIndex { get { return Steps.FindIndex(x => !x.Done); } }
        public bool AskDefenseComplete { get { return Steps.Count == 6 && Steps[4].Done && !Steps[5].Done; } }

    }
}