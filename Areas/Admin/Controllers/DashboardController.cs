using System.Web.Mvc;

namespace Subsystem1.Areas.Admin.Controllers
{
    public class DashboardController : Controller
    {
        public ActionResult dashboard()
        {
            return View();
        }
    }
}