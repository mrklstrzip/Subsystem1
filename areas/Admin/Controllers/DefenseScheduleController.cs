using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.Admin.Controllers
{
    // View-only copy of the Capstone Coordinator Defense Schedule page for the Admin. Signed-in check is done by AdminAuthorize.
    public class DefenseScheduleController : Controller
    {
        public ActionResult defenseschedule()
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            return View(CoordinatorData.Load(false));
        }
    }
}
