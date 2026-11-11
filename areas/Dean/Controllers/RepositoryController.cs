using System;
using System.Linq;
using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using Subsystem1.Areas.Dean.Models;

namespace Subsystem1.Areas.Dean.Controllers
{
    // Dean-facing, read-only view of Subsystem 3 clearances from the shared Mongo database.
    // Schedule approvals remain in the existing Subsystem 1 controllers.
    public sealed class RepositoryController : DeanControllerBase
    {
        public ActionResult Index(string q, string program, int? year, string filing, string adviser, string sort)
        {
            var rows = Subsystem3Clearances.ReadCleared();
            var all = rows.ToList();
            if (!String.IsNullOrWhiteSpace(q))
            {
                string term = q.Trim();
                rows = rows.Where(x => Contains(x.Title, term) || Contains(x.Program, term) ||
                    Contains(x.Section, term) || Contains(x.Id, term) || Contains(x.ApprovedManuscriptName, term) ||
                    Contains(x.Authors, term) || Contains(x.Adviser, term))
                    .ToList();
            }
            if (!String.IsNullOrWhiteSpace(program)) rows = rows.Where(x => String.Equals(x.Program, program, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!String.IsNullOrWhiteSpace(adviser)) rows = rows.Where(x => String.Equals(x.Adviser, adviser, StringComparison.OrdinalIgnoreCase)).ToList();
            if (year.HasValue) rows = rows.Where(x => (x.FiledAt ?? x.DisseminatedAt)?.Year == year.Value).ToList();
            if (!String.IsNullOrWhiteSpace(filing)) rows = rows.Where(x => String.Equals(x.IsFiledImport || x.Status == "Filed" ? "Filed" : "Disseminated", filing, StringComparison.OrdinalIgnoreCase)).ToList();
            if (sort == "oldest") rows = rows.OrderBy(x => x.FiledAt ?? x.DisseminatedAt).ToList();
            else if (sort == "title") rows = rows.OrderBy(x => x.Title).ToList();
            else rows = rows.OrderByDescending(x => x.FiledAt ?? x.DisseminatedAt).ToList();
            ViewBag.Title = "Cleared Manuscripts";
            return View(new DeanRepositoryVm
            {
                Query = q ?? "", Items = rows, Program = program ?? "", Adviser = adviser ?? "", Year = year, Filing = filing ?? "",
                Sort = sort ?? "recent",
                Programs = all.Select(x => x.Program).Where(x => !String.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                Years = all.Select(x => (x.FiledAt ?? x.DisseminatedAt)?.Year).Where(x => x.HasValue).Select(x => x.Value).Distinct().OrderByDescending(x => x).ToList(),
                Filings = new[] { "Filed", "Disseminated" },
                Advisers = all.Select(x => x.Adviser).Where(x => !String.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList()
            });
        }

        public ActionResult Paper(string id)
        {
            ClearedSubmission item = Find(id);
            if (item == null) return HttpNotFound();
            ViewBag.Title = item.Title;
            return View(new DeanManuscriptVm { Item = item });
        }

        public ActionResult Read(string id)
        {
            ClearedSubmission item = Find(id);
            if (item == null) return HttpNotFound();
            ViewBag.Title = "Read Manuscript";
            return View(new DeanManuscriptVm { Item = item });
        }

        private static ClearedSubmission Find(string id)
        {
            return Subsystem3Clearances.ReadCleared().FirstOrDefault(x =>
                String.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static bool Contains(string value, string term)
        {
            return !String.IsNullOrEmpty(value) && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
