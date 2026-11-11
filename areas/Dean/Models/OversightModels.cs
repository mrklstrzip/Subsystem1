using System;
using System.Collections.Generic;

namespace Subsystem1.Areas.Dean.Models
{
    public class OversightRecord
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Program { get; set; }
        public string Adviser { get; set; }
        public string Abstract { get; set; }
        public string Keywords { get; set; }
        public string Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    public class OversightVm
    {
        public string PageTitle { get; set; }
        public string Query { get; set; }
        public string Program { get; set; }
        public string Status { get; set; }
        public int Total { get; set; }
        public int Submitted { get; set; }
        public int Approved { get; set; }
        public int Returned { get; set; }
        public double AverageAgeDays { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string ReportType { get; set; }
        public int? Batch { get; set; }
        public List<int> Batches { get; set; } = new List<int>();
        public List<ReportBatchSummary> BatchSummaries { get; set; } = new List<ReportBatchSummary>();
        public List<OversightRecord> Records { get; set; } = new List<OversightRecord>();
        public List<KeyValuePair<string, int>> Programs { get; set; } = new List<KeyValuePair<string, int>>();
    }

    public class ReportBatchSummary
    {
        public int Batch { get; set; }
        public int Total { get; set; }
        public int Cleared { get; set; }
        public int Pending { get; set; }
        public int Incomplete { get; set; }
        public int CompletionRate { get; set; }
    }
}
