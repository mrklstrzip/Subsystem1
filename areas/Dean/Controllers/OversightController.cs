using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.Dean.Models;
using Subsystem1.Data;

namespace Subsystem1.Areas.Dean.Controllers
{
    // Read-only oversight pages added from Subsystem 3. Existing Subsystem 1
    // approval and defense-scheduling actions remain the source of truth.
    public class OversightController : DeanControllerBase
    {
        private static string Read(BsonDocument d, string name)
        {
            BsonValue v;
            return d != null && d.TryGetValue(name, out v) && v != null && !v.IsBsonNull ? v.ToString() : "";
        }
        private static DateTime? Date(BsonDocument d, string name)
        {
            BsonValue v;
            if (d != null && d.TryGetValue(name, out v) && v != null && v.IsBsonDateTime) return v.ToUniversalTime().AddHours(8);
            return null;
        }
        private static List<OversightRecord> Load()
        {
            var docs = SharedDb.Titles().Find(FilterDefinition<BsonDocument>.Empty).Limit(2000).ToList();
            var records = docs.Select(d => new OversightRecord {
                Id = SharedDb.DocId(d), Title = Read(d,"title"),
                Authors = Read(d,"authors") != "" ? Read(d,"authors") : Read(d,"projectManager"),
                Program = Read(d,"program"), Adviser = Read(d,"adviser"), Abstract = Read(d,"abstract"),
                Keywords = Read(d,"keywords"), Status = Read(d,"status"),
                SubmittedAt = Date(d,"submittedAt"), UpdatedAt = Date(d,"updatedAt")
            }).Where(x => !String.IsNullOrWhiteSpace(x.Title)).ToList();

            // The Subsystem 3 Dean package reads the live capstone workflow. Keep the existing
            // Subsystem 1 title records visible, and add current Subsystem 3 submissions from
            // the same central database without creating a second handoff or local copy.
            var s3 = Db.Database.GetCollection<BsonDocument>("capstone_submissions")
                .Find(FilterDefinition<BsonDocument>.Empty).Limit(3000).ToList();
            foreach (var d in s3)
            {
                BsonDocument state = Child(d, "subsystem3");
                BsonDocument clearance = Child(state, "CapstoneClearance");
                string title = First(Read(d, "title"), First(Read(state, "Title"), Read(state, "title")));
                if (String.IsNullOrWhiteSpace(title)) continue;
                string id = d.Contains("_id") ? d["_id"].ToString() : "";
                DateTime? submittedAt = Date(state, "SubmittedAt") ?? Date(d, "submittedAt");
                string submissionStatus = Read(state, "Status");
                var validationSummaries = ChildDocuments(state, "ValidationSummaries");
                var completedReviews = validationSummaries.Select(summary => new
                    {
                        Submitted = Date(summary, "SubmittedAt") ?? Date(summary, "submittedAt"),
                        Completed = Date(summary, "CompletedAt") ?? Date(summary, "completedAt")
                    })
                    .Where(summary => summary.Submitted.HasValue && summary.Completed.HasValue && summary.Completed >= summary.Submitted)
                    .Select(summary => (summary.Completed.Value - summary.Submitted.Value).TotalDays)
                    .ToList();
                DateTime? lastReviewAt = validationSummaries
                    .Select(summary => Date(summary, "CompletedAt") ?? Date(summary, "completedAt"))
                    .Where(date => date.HasValue)
                    .OrderByDescending(date => date.Value)
                    .FirstOrDefault();
                var openReview = validationSummaries
                    .Where(summary => !(Date(summary, "CompletedAt") ?? Date(summary, "completedAt")).HasValue)
                    .Select(summary => Date(summary, "SubmittedAt") ?? Date(summary, "submittedAt"))
                    .Where(date => date.HasValue)
                    .OrderByDescending(date => date.Value)
                    .FirstOrDefault();
                DateTime? waitingSince = IsPending(submissionStatus) ? (openReview ?? submittedAt) : null;
                records.RemoveAll(x => String.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                records.Add(new OversightRecord {
                    Id = id, Title = title,
                    Authors = First(Read(d, "student"), First(Read(d, "studentName"), Read(clearance, "GroupName"))),
                    Program = First(Read(state, "Program"), First(Read(clearance, "Program"), Read(d, "program"))),
                    Adviser = First(Read(d, "adviser"), Read(state, "Adviser")),
                    Abstract = First(Read(state, "Abstract"), Read(d, "abstract")),
                    Keywords = First(Read(state, "Keywords"), Read(d, "keywords")),
                    Status = submissionStatus,
                    SubmittedAt = submittedAt,
                    UpdatedAt = Date(state, "FiledAt") ?? Date(state, "DisseminatedAt") ?? Date(state, "UpdatedAt"),
                    LastReviewAt = lastReviewAt,
                    ReviewTimeDays = completedReviews.Count == 0 ? (double?)null : completedReviews.Average(),
                    WaitingPeriodDays = waitingSince.HasValue ? Math.Max(0, (SharedDb.NowManila() - waitingSince.Value).TotalDays) : (double?)null,
                    RecheckingCycles = Math.Max(0, validationSummaries.Count - 1)
                });
            }
            return records;
        }

        private static BsonDocument Child(BsonDocument document, string key)
        {
            BsonValue value;
            return document != null && document.TryGetValue(key, out value) && value != null && value.IsBsonDocument
                ? value.AsBsonDocument : null;
        }

        private static List<BsonDocument> ChildDocuments(BsonDocument document, string key)
        {
            BsonValue value;
            return document != null && document.TryGetValue(key, out value) && value != null && value.IsBsonArray
                ? value.AsBsonArray.Where(item => item != null && item.IsBsonDocument).Select(item => item.AsBsonDocument).ToList()
                : new List<BsonDocument>();
        }

        private static string First(string first, string second)
        {
            return String.IsNullOrWhiteSpace(first) ? (second ?? "") : first;
        }
        private ActionResult Page(string title, string q, string program, string status, string viewName = "Index", DateTime? from = null, DateTime? to = null, string reportType = null, int? batch = null, string ranking = null)
        {
            var all = Load();
            IEnumerable<OversightRecord> filtered = all;
            if (from.HasValue) filtered = filtered.Where(x => (x.UpdatedAt ?? x.SubmittedAt).HasValue && (x.UpdatedAt ?? x.SubmittedAt).Value.Date >= from.Value.Date);
            if (to.HasValue) filtered = filtered.Where(x => (x.UpdatedAt ?? x.SubmittedAt).HasValue && (x.UpdatedAt ?? x.SubmittedAt).Value.Date <= to.Value.Date);
            if (batch.HasValue) filtered = filtered.Where(x => (x.SubmittedAt ?? x.UpdatedAt).HasValue && (x.SubmittedAt ?? x.UpdatedAt).Value.Year == batch.Value);
            if (!String.IsNullOrWhiteSpace(q)) filtered = filtered.Where(x => (x.Title+" "+x.Authors+" "+x.Keywords+" "+x.Adviser).IndexOf(q.Trim(), StringComparison.OrdinalIgnoreCase)>=0);
            if (!String.IsNullOrWhiteSpace(program)) filtered = filtered.Where(x => String.Equals(x.Program,program,StringComparison.OrdinalIgnoreCase));
            if (!String.IsNullOrWhiteSpace(status)) filtered = filtered.Where(x => String.Equals(x.Status,status,StringComparison.OrdinalIgnoreCase));
            var rows = String.IsNullOrWhiteSpace(ranking)
                ? filtered.OrderByDescending(x => x.UpdatedAt ?? x.SubmittedAt ?? DateTime.MinValue).ToList()
                : ranking == "review"
                    ? filtered.OrderByDescending(x => x.ReviewTimeDays ?? -1).ThenBy(x => x.Title).ToList()
                    : ranking == "rechecks"
                        ? filtered.OrderByDescending(x => x.RecheckingCycles).ThenBy(x => x.Title).ToList()
                        : filtered.OrderByDescending(x => x.WaitingPeriodDays ?? -1).ThenBy(x => x.Title).ToList();
            var vm = new OversightVm { PageTitle=title, Query=q, Program=program, Status=status, Records=rows, From=from, To=to, ReportType=reportType, Batch=batch, Total=rows.Count,
                Submitted=rows.Count(x=>String.Equals(x.Status,"submitted",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Status,"pending",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Status,"validated",StringComparison.OrdinalIgnoreCase)),
                Approved=rows.Count(x=>String.Equals(x.Status,"approved",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Status,"cleared",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Status,"disseminated",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Status,"filed",StringComparison.OrdinalIgnoreCase)),
                Returned=rows.Count(x=>String.Equals(x.Status,"returned",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Status,"revision required",StringComparison.OrdinalIgnoreCase)),
                AverageAgeDays=rows.Where(x=>x.SubmittedAt.HasValue).Select(x=>(DateTime.UtcNow.AddHours(8)-x.SubmittedAt.Value).TotalDays).DefaultIfEmpty(0).Average() };
            var reviewValues = rows.Where(x => x.ReviewTimeDays.HasValue).Select(x => x.ReviewTimeDays.Value).ToList();
            var waitingValues = rows.Where(x => x.WaitingPeriodDays.HasValue).Select(x => x.WaitingPeriodDays.Value).ToList();
            var longestWait = rows.Where(x => x.WaitingPeriodDays.HasValue).OrderByDescending(x => x.WaitingPeriodDays.Value).FirstOrDefault();
            vm.AverageReviewDays = reviewValues.Count == 0 ? (double?)null : reviewValues.Average();
            vm.AverageWaitingDays = waitingValues.Count == 0 ? (double?)null : waitingValues.Average();
            vm.AverageRecheckingCycles = rows.Count == 0 ? 0 : rows.Average(x => x.RecheckingCycles);
            vm.LongestWaitDays = longestWait == null ? (double?)null : longestWait.WaitingPeriodDays;
            vm.LongestWaitTitle = longestWait == null ? "No pending group" : longestWait.Title;
            vm.PerformanceBatches = rows.Where(x => (x.SubmittedAt ?? x.UpdatedAt).HasValue)
                .GroupBy(x => (x.SubmittedAt ?? x.UpdatedAt).Value.Year).OrderBy(x => x.Key)
                .Select(group => new PerformanceBatchSummary {
                    Batch = group.Key,
                    AverageReviewDays = group.Any(x => x.ReviewTimeDays.HasValue) ? (double?)group.Where(x => x.ReviewTimeDays.HasValue).Average(x => x.ReviewTimeDays.Value) : null,
                    AverageWaitingDays = group.Any(x => x.WaitingPeriodDays.HasValue) ? (double?)group.Where(x => x.WaitingPeriodDays.HasValue).Average(x => x.WaitingPeriodDays.Value) : null
                }).ToList();
            vm.RecheckingDistribution = new List<RecheckingSummary> {
                new RecheckingSummary { Label = "0 Cycles", Count = rows.Count(x => x.RecheckingCycles == 0) },
                new RecheckingSummary { Label = "1 Cycle", Count = rows.Count(x => x.RecheckingCycles == 1) },
                new RecheckingSummary { Label = "2 Cycles", Count = rows.Count(x => x.RecheckingCycles == 2) },
                new RecheckingSummary { Label = "3+ Cycles", Count = rows.Count(x => x.RecheckingCycles >= 3) }
            };
            vm.SlowestGroups = rows.Take(10).ToList();
            ViewBag.PerformanceSort = ranking ?? "wait";
            vm.Programs = all.Where(x=>!String.IsNullOrWhiteSpace(x.Program)).GroupBy(x=>x.Program).OrderBy(x=>x.Key).Select(g=>new KeyValuePair<string,int>(g.Key,g.Count())).ToList();
            vm.Batches = all.Select(x => (x.SubmittedAt ?? x.UpdatedAt)?.Year).Where(x => x.HasValue).Select(x => x.Value).Distinct().OrderByDescending(x => x).ToList();
            vm.BatchSummaries = rows.Where(x => (x.SubmittedAt ?? x.UpdatedAt).HasValue)
                .GroupBy(x => (x.SubmittedAt ?? x.UpdatedAt).Value.Year).OrderBy(g => g.Key)
                .Select(g => {
                    int cleared = g.Count(x => IsCleared(x.Status));
                    int pending = g.Count(x => IsPending(x.Status));
                    int incomplete = g.Count(x => IsIncomplete(x.Status));
                    return new ReportBatchSummary { Batch = g.Key, Total = g.Count(), Cleared = cleared, Pending = pending, Incomplete = incomplete, CompletionRate = g.Count() == 0 ? 0 : (int)Math.Round(100.0 * cleared / g.Count()) };
                }).ToList();
            ViewBag.Title=title; return View(viewName,vm);
        }
        private static bool IsCleared(string status) { return new[] { "approved", "cleared", "disseminated", "filed" }.Contains(status ?? "", StringComparer.OrdinalIgnoreCase); }
        private static bool IsPending(string status) { return new[] { "submitted", "pending", "validated", "reviewing", "under validation" }.Contains(status ?? "", StringComparer.OrdinalIgnoreCase); }
        private static bool IsIncomplete(string status) { return new[] { "returned", "revision required", "rejected", "draft" }.Contains(status ?? "", StringComparer.OrdinalIgnoreCase); }
        public ActionResult Index(string q, string program) { return Page("Research Oversight",q,program,null); }
        public ActionResult Repository(string q, string program) { return Page("Research Repository",q,program,null); }
        public ActionResult Records(string q, string program, string status) { return Page("Submission & Clearance Records",q,program,status,"Records"); }
        public ActionResult Monitoring(string q, string program) { return Page("Research Monitoring",q,program,null); }
        public ActionResult Cohort(string q, string program) { return Page("Cohort Comparison",q,program,null,"Monitoring"); }
        public ActionResult Exceptions(string q, string program, string status, int? batch) { return Page("Compliance Exceptions",q,program,status,"Monitoring", batch: batch); }
        public ActionResult Performance(string q, string program, string status, int? batch, string sort) { return Page("Processing Performance",q,program,status,"Monitoring", batch: batch, ranking: String.IsNullOrWhiteSpace(sort) ? "wait" : sort); }
        public ActionResult Reports(DateTime? from, DateTime? to, int? batch, string program, string type)
        {
            if (!from.HasValue) from = new DateTime(2022, 1, 1);
            if (!to.HasValue) to = SharedDb.NowManila().Date;
            return Page("Reports", null, program, null, "Reports", from, to, type, batch);
        }

        public ActionResult ExportReport(DateTime? from, DateTime? to, int? batch, string program, string type)
        {
            var data = Load().Where(x => !from.HasValue || ((x.UpdatedAt ?? x.SubmittedAt).HasValue && (x.UpdatedAt ?? x.SubmittedAt).Value.Date >= from.Value.Date))
                .Where(x => !to.HasValue || ((x.UpdatedAt ?? x.SubmittedAt).HasValue && (x.UpdatedAt ?? x.SubmittedAt).Value.Date <= to.Value.Date))
                .Where(x => !batch.HasValue || ((x.SubmittedAt ?? x.UpdatedAt).HasValue && (x.SubmittedAt ?? x.UpdatedAt).Value.Year == batch.Value))
                .Where(x => String.IsNullOrWhiteSpace(program) || String.Equals(x.Program, program, StringComparison.OrdinalIgnoreCase)).ToList();
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Batch,Submission ID,Title,Program,Status,Submitted,Last Updated");
            foreach (var row in data)
            {
                int? rowBatch = (row.SubmittedAt ?? row.UpdatedAt)?.Year;
                csv.AppendLine(String.Join(",", new[] { rowBatch.HasValue ? rowBatch.Value.ToString() : "", Csv(row.Id), Csv(row.Title), Csv(row.Program), Csv(row.Status), Csv(row.SubmittedAt.HasValue ? row.SubmittedAt.Value.ToString("yyyy-MM-dd") : ""), Csv(row.UpdatedAt.HasValue ? row.UpdatedAt.Value.ToString("yyyy-MM-dd") : "") }));
            }
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "dean-compliance-report.csv");
        }
        private static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }

        // Combined audit timeline from Dean oversight events and Coordinator screening/case events.
        // Both collections live in the same Subsystem 1 MongoDB database.
        public ActionResult Audit()
        {
            var events = new List<BsonDocument>();
            var collections = new[] { "dean_audit", "capstone_audit" };
            foreach (var name in collections)
            {
                var collection = Db.Database.GetCollection<BsonDocument>(name);
                var rows = collection.Find(FilterDefinition<BsonDocument>.Empty)
                    .Sort(Builders<BsonDocument>.Sort.Descending("at")).Limit(300).ToList();
                foreach (var row in rows)
                {
                    var copy = row.DeepClone().AsBsonDocument;
                    copy["auditSource"] = name == "dean_audit" ? "Dean" : "Capstone Coordinator";
                    events.Add(copy);
                }
            }
            // Subsystem 3's Dean module stored its audit timeline as JSON documents
            // in the shared settings collection (kind = deanAudit). Read that format too.
            var settings = Db.Database.GetCollection<BsonDocument>("settings");
            var savedDeanEvents = settings.Find(Builders<BsonDocument>.Filter.Eq("kind", "deanAudit")).Limit(300).ToList();
            foreach (var saved in savedDeanEvents)
            {
                BsonValue json;
                if (!saved.TryGetValue("json", out json) || json == null || !json.IsString) continue;
                try
                {
                    var source = BsonDocument.Parse(json.AsString);
                    var normalized = new BsonDocument
                    {
                        { "auditSource", "Dean" },
                        { "actor", source.GetValue("Account", "").ToString() },
                        { "action", source.GetValue("Action", "").ToString() },
                        { "target", source.GetValue("RecordTitle", source.GetValue("RecordId", "")).ToString() },
                        { "details", source.GetValue("New", source.GetValue("Prev", "")).ToString() }
                    };
                    BsonValue when;
                    if (source.TryGetValue("When", out when)) normalized["at"] = when;
                    events.Add(normalized);
                }
                catch { /* Ignore malformed legacy audit rows; keep the rest of the timeline available. */ }
            }
            ViewBag.Events = events.OrderByDescending(e => Date(e, "at") ?? DateTime.MinValue).Take(500).ToList();
            ViewBag.Title = "Dean Audit History";
            return View("Audit");
        }
    }
}
