using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;

namespace Subsystem1.Areas.Admin.Controllers
{
    public class AdminRequestItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProgramName { get; set; }
        public string AdviserName { get; set; }
        public bool Created { get; set; }
        public DateTime At { get; set; }   // Philippine time
    }

    // Draws the account requests in the Admin notification drawer (see _AdminLayout.cshtml),
    // so the shared Notifs.js works on the finished HTML without any change.
    //   Forwarded = an Adviser reviewed the request and sent it here -> "Create"
    //   Created   = the account was made (shown for a while as history)
    public class NotificationController : Controller
    {
        private static string Str(BsonDocument d, string name)
        {
            BsonValue v;
            return d.TryGetValue(name, out v) && v != null && v.IsString ? v.AsString : "";
        }

        private static DateTime When(BsonDocument d, params string[] names)
        {
            foreach (string n in names)
            {
                BsonValue v;
                if (d.TryGetValue(n, out v) && v != null && v.IsBsonDateTime) return v.ToUniversalTime().AddHours(8);
            }
            return DateTime.UtcNow.AddHours(8);
        }

        private static string Program(string code)
        {
            switch ((code ?? "").Trim().ToUpperInvariant())
            {
                case "BSIT": return "BS Information Technology";
                case "BSCS": return "BS Computer Science";
                case "BSIS": return "BS Information System";
                default: return code ?? "";
            }
        }

        [ChildActionOnly]
        public ActionResult items()
        {
            var requests = Db.Database.GetCollection<BsonDocument>("account_requests");
            var f = Builders<BsonDocument>.Filter;

            var forwarded = requests.Find(f.Eq("status", "Forwarded")).ToList();
            var created = requests.Find(f.Eq("status", "Created")).ToList();

            Func<BsonDocument, string> nameOf = d =>
                (Str(d, "firstName") + " " + Str(d, "lastName") + (Str(d, "suffix") != "" ? " " + Str(d, "suffix") : "")).Trim();

            var list = forwarded
                .Select(d => new AdminRequestItem
                {
                    Id = d["_id"].ToString(),
                    Name = nameOf(d),
                    ProgramName = Program(Str(d, "program")),
                    AdviserName = Str(d, "adviserName"),
                    Created = false,
                    At = When(d, "forwardedAt", "createdAt")
                })
                .OrderByDescending(i => i.At)
                .ToList();

            list.AddRange(created
                .Select(d => new AdminRequestItem
                {
                    Id = d["_id"].ToString(),
                    Name = nameOf(d),
                    ProgramName = Program(Str(d, "program")),
                    AdviserName = Str(d, "adviserName"),
                    Created = true,
                    At = When(d, "createdAt", "forwardedAt")
                })
                .OrderByDescending(i => i.At)
                .Take(8));

            return PartialView("_RequestItems", list);
        }
    }
}
