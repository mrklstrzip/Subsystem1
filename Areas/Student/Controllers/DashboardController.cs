using System.Web.Mvc;

namespace Subsystem1.Areas.Student.Controllers
{
    public class DashboardController : Controller
    {
        public ActionResult dashboard()
        {
             var model = new StudentDashboardViewModel();
             
             return View(model);
        }
    }
}
