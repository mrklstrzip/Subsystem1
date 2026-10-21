using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Dean.Models
{
    public class DashboardModels
    {
    }
    public class DashboardRow
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Section { get; set; }   // e.g. "BSIT-4A"
        public string Author { get; set; }
        public string Adviser { get; set; }
        public string Date { get; set; }

        // only used by the Upcoming table
        public string Room { get; set; }
        public string Time { get; set; }

        public string TagClass
        {
            get
            {
                var program = (Section ?? "").Split('-')[0].ToUpper();
                if (program == "BSCS") return "tag-red";
                if (program == "BSIS") return "tag-purple";
                return "tag-gray";
            }
        }
    }

    public class ActivityRow
    {
        public string Time { get; set; }
        public string Text { get; set; }
    }

    public class DashboardViewModel
    {
        public List<DashboardRow> ToApprove { get; set; } = new List<DashboardRow>();
        public List<DashboardRow> Upcoming { get; set; } = new List<DashboardRow>();
        public int TotalDefenses { get; set; }
        public List<ActivityRow> RecentActivities { get; set; } = new List<ActivityRow>();
    }
}