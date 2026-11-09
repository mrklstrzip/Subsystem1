using System.Web.Mvc;
using Subsystem1.Areas.Dean.Models;

namespace Subsystem1.Areas.Dean.Controllers
{
    // Draws the notification drawer's items inside the layout (see _DeanLayout.cshtml).
    public class NotificationController : DeanControllerBase
    {
        [ChildActionOnly]
        public ActionResult items()
        {
            return PartialView("_NotificationItems", DeanNotifications.Load(Me));
        }
    }
}
