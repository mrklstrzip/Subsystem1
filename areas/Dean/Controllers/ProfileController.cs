using System.Web.Mvc;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class ProfileController : DeanControllerBase
    {
        public ActionResult profile()
        {
            ViewBag.Title = "Profile";
            return View();
        }
    }
}
