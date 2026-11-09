using System.Web.Mvc;
using Subsystem1.Areas.Dean.Models;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DefenseScheduleController : DeanControllerBase
    {
        // GET: Dean/DefenseSchedule
        public ActionResult DefenseScheduleView(string search, string program = "All",
                                                string status = "All", string adviser = "All")
        {
            ViewBag.Title = "Defense Schedule";
            return View(DeanData.Defenses(search, program, status, adviser));
        }

        public ActionResult Index()
        {
            return RedirectToAction("DefenseScheduleView");
        }
    }
}
