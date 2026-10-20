using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Student.ViewModels
{
    public class StudentDashboardViewModel
    {
        public string UserRole { get; set; } = "Project Manager";

        // Manuscript status card
        public string ManuscriptStatus { get; set; }
        public int? Stage { get; set; }
        public int? TotalStages { get; set; }
        public DateTime? StatusDate { get; set; }

        // Defense card
        public DateTime? DefenseDate { get; set; }
        public string DefenseVenue { get; set; }

        public List<PanelistItem> Panel { get; set; } = new List<PanelistItem>();
        public List<ActivityItem> Activity { get; set; } = new List<ActivityItem>();
    }

    public class PanelistItem
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }   // new (optional)
        public string PhotoUrl { get; set; }

        public string Initials
        {
            get
            {
                var letters = (Name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => !p.EndsWith(".")).Take(2).Select(p => p[0]).ToArray();
                return new string(letters);
            }
        }
    }

    public class ActivityItem
    {
        /// <summary>Wrap text in **double asterisks** to show it in bold on the Student pages.</summary>
        public string Description { get; set; }
        public DateTime Date { get; set; }
    }
}
