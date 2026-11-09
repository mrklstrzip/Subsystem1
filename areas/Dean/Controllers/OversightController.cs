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
            return docs.Select(d => new OversightRecord {
                Id = SharedDb.DocId(d), Title = Read(d,"title"),
                Authors = Read(d,"authors") != "" ? Read(d,"authors") : Read(d,"projectManager"),
                Program = Read(d,"program"), Adviser = Read(d,"adviser"), Abstract = Read(d,"abstract"),
                Keywords = Read(d,"keywords"), Status = Read(d,"status"),
                SubmittedAt = Date(d,"submittedAt"), UpdatedAt = Date(d,"updatedAt")
            }).Where(x => !String.IsNullOrWhiteSpace(x.Title)).ToList();
        }
        private ActionResult Page(string title, string q, string program, string status)
        {
            var all = Load();
            IEnumerable<OversightRecord> filtered = all;
            if (!String.IsNullOrWhiteSpace(q)) filtered = filtered.Where(x => (x.Title+" "+x.Authors+" "+x.Keywords+" "+x.Adviser).IndexOf(q.Trim(), StringComparison.OrdinalIgnoreCase)>=0);
            if (!String.IsNullOrWhiteSpace(program)) filtered = filtered.Where(x => String.Equals(x.Program,program,StringComparison.OrdinalIgnoreCase));
            if (!String.IsNullOrWhiteSpace(status)) filtered = filtered.Where(x => String.Equals(x.Status,status,StringComparison.OrdinalIgnoreCase));
            var rows = filtered.OrderByDescending(x => x.UpdatedAt ?? x.SubmittedAt ?? DateTime.MinValue).ToList();
            var vm = new OversightVm { PageTitle=title, Query=q, Program=program, Status=status, Records=rows, Total=all.Count,
                Submitted=all.Count(x=>x.Status.Equals("submitted",StringComparison.OrdinalIgnoreCase)||x.Status.Equals("pending",StringComparison.OrdinalIgnoreCase)),
                Approved=all.Count(x=>x.Status.Equals("approved",StringComparison.OrdinalIgnoreCase)||x.Status.Equals("cleared",StringComparison.OrdinalIgnoreCase)),
                Returned=all.Count(x=>x.Status.Equals("returned",StringComparison.OrdinalIgnoreCase)||x.Status.Equals("revision required",StringComparison.OrdinalIgnoreCase)),
                AverageAgeDays=all.Where(x=>x.SubmittedAt.HasValue).Select(x=>(DateTime.UtcNow.AddHours(8)-x.SubmittedAt.Value).TotalDays).DefaultIfEmpty(0).Average() };
            vm.Programs = all.Where(x=>!String.IsNullOrWhiteSpace(x.Program)).GroupBy(x=>x.Program).OrderBy(x=>x.Key).Select(g=>new KeyValuePair<string,int>(g.Key,g.Count())).ToList();
            ViewBag.Title=title; return View("Index",vm);
        }
        public ActionResult Index(string q, string program) { return Page("Research Oversight",q,program,null); }
        public ActionResult Repository(string q, string program) { return Page("Research Repository",q,program,null); }
        public ActionResult Records(string q, string program, string status) { return Page("Submission Records",q,program,status); }
        public ActionResult Monitoring(string q, string program) { return Page("Research Monitoring",q,program,null); }
        public ActionResult Cohort(string q, string program) { return Page("Cohort Monitoring",q,program,null); }
        public ActionResult Exceptions(string q, string program, string status) { return Page("Compliance Exceptions",q,program,status); }
        public ActionResult Performance(string q, string program, string status) { return Page("Processing Performance",q,program,status); }

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
