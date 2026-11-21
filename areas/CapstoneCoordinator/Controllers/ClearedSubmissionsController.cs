using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using System;
using System.Linq;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ClearedSubmissionsController : CoordinatorControllerBase
    {
        public ActionResult Index()
        {
            return View(Subsystem3Clearances.ReadCompleted());
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
    }
}
