using System.Web.Mvc;
using Subsystem1.Areas.Dean.Models;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DashboardController : DeanControllerBase
    {
        public ActionResult dashboard()
        {
            ViewBag.Title = "Dashboard";
            return View(DeanData.Dashboard());
        }
    }
}
