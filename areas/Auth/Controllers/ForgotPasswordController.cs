using System.Web.Mvc;

namespace Subsystem1.Areas.Auth.Controllers
{
    public class ForgotPasswordController : Controller
    {
        public ActionResult forgotpassword()
        {
            return View();
        }
    }
}