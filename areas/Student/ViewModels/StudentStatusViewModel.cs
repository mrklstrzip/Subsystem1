using System;
using System.Collections.Generic;

namespace Subsystem1.Areas.Student.ViewModels
{
    public class StageInfo
    {
        public string Description { get; set; }
        public DateTime? Date { get; set; }
    }

    public class StudentStatusViewModel
    {
        public int Stage { get; set; }
        public List<string> StageLabels { get; set; } = new List<string>();

        public List<StageInfo> StageInfos { get; set; } = new List<StageInfo>();

        public string ManuscriptTitle { get; set; }
        public List<string> Panelists { get; set; } = new List<string>();
        public string Room { get; set; }
        public DateTime? DefenseDate { get; set; }

        public List<ActivityItem> Activity { get; set; } = new List<ActivityItem>();

        public int TotalStages { get { return StageLabels.Count; } }

        public bool AwaitingDefenseConfirmation
        {
            get { return TotalStages > 0 && Stage == TotalStages; }
        }
    }
}