using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using static Subsystem1.Data.SharedDb;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class DashboardController : CoordinatorControllerBase
    {
        public ActionResult dashboard()
        {
            ViewBag.LastName = Str(Me, "lastName");
            return View(CoordinatorData.Load(true));
        }
    }
}
