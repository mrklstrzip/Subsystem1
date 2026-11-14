using System.Web.Mvc;
using Subsystem1.Areas.Dean.Models;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DashboardController : DeanControllerBase
    {
        public ActionResult dashboard()
        {
            ViewBag.Title = "Dashboard";
            var model = DeanData.Dashboard();
            model.ClearedManuscripts = Subsystem3Clearances.ReadCleared().Count;
            return View(model);
        }
    }
}
