using System.Web.Mvc;

namespace Subsystem1.Areas.Admin.Controllers
{
    // View-only copy of the Campus Director Room Schedule page for the Admin. Signed-in check is done by AdminAuthorize.
    public class RoomScheduleController : Controller
    {
        public ActionResult roomschedule()
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            return View();
        }
    }
}
