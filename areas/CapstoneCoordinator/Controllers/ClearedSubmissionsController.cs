using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using System;
using System.Linq;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ClearedSubmissionsController : CoordinatorControllerBase
    {
        public ActionResult Index(string q, string program, string status, string adviser, bool showArchived = false)
        {
            var items = Subsystem3Clearances.ReadCompleted();
            var allItems = items.ToList();
            showArchived = showArchived || String.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
            ViewBag.Query = q ?? "";
            ViewBag.Program = program ?? "";
            ViewBag.Status = status ?? "";
            ViewBag.Adviser = adviser ?? "";
            ViewBag.ShowArchived = showArchived;
            ViewBag.ArchivedCount = allItems.Count(item => item.IsArchived);
            ViewBag.Programs = allItems.Select(item => item.Program).Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
            ViewBag.Advisers = allItems.Select(item => item.Adviser).Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
            ViewBag.Statuses = allItems.Select(StatusOf).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
            if (!showArchived) items = items.Where(item => !item.IsArchived).ToList();
            if (!String.IsNullOrWhiteSpace(q))
            {
                string term = q.Trim();
                items = items.Where(item => Contains(item.Title, term) || Contains(item.Authors, term) || Contains(item.Adviser, term) ||
                    Contains(item.Program, term) || Contains(item.Section, term) || Contains(item.ClearanceGroup, term) ||
                    (item.Keywords != null && item.Keywords.Any(keyword => Contains(keyword, term)))).ToList();
            }
            if (!String.IsNullOrWhiteSpace(program)) items = items.Where(item => String.Equals(item.Program, program, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!String.IsNullOrWhiteSpace(status)) items = items.Where(item => String.Equals(StatusOf(item), status, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!String.IsNullOrWhiteSpace(adviser)) items = items.Where(item => String.Equals(item.Adviser, adviser, StringComparison.OrdinalIgnoreCase)).ToList();
            return View(items);
        }

        public ActionResult ViewManuscript(string id)
        {
            var item = FindCompleted(id);
            if (item == null) return HttpNotFound();
            ViewBag.Title = item.Title;
            return View("ViewManuscript", item);
        }

        [HttpGet]
        public ActionResult Manuscript(string id)
        {
            var item = FindCompleted(id);
            if (item == null) return HttpNotFound();
            if (String.IsNullOrWhiteSpace(item.ApprovedManuscriptName) ||
                !item.ApprovedManuscriptName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(415, "The approved Subsystem 3 file is not a PDF.");

            try
            {
                byte[] bytes = Subsystem3Clearances.ReadApprovedManuscript(item);
                if (bytes == null) return new HttpStatusCodeResult(404, "The approved Subsystem 3 manuscript is unavailable or failed integrity verification.");
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
                Response.ContentType = "application/pdf";
                Response.AddHeader("Content-Disposition", "inline; filename=manuscript.pdf");
                Response.AddHeader("X-Content-Type-Options", "nosniff");
                return File(bytes, "application/pdf");
            }
            catch
            {
                return new HttpStatusCodeResult(502, "Subsystem 3 file storage could not provide this manuscript.");
            }
        }

        private static ClearedSubmission FindCompleted(string id)
        {
            return Subsystem3Clearances.ReadCompleted().FirstOrDefault(item =>
                String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static string StatusOf(ClearedSubmission item)
        {
            return item.IsArchived ? "Archived" : item.IsHistorical ? "Historical" : "Filed";
        }

        private static bool Contains(string value, string term)
        {
            return !String.IsNullOrEmpty(value) && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
