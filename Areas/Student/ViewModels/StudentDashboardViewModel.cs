using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Student.ViewModels
{
    public class StudentDashboardViewModel
    {
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
        public string Department { get; set; }
        public string PhotoUrl { get; set; }
    }

    public class ActivityItem
    {
        public string Description { get; set; }
        public DateTime Date { get; set; }
    }
}
