using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    // Draws the notification drawer's items inside the layout (see _CapstoneCoordinatorLayout.cshtml).
    public class NotificationController : CoordinatorControllerBase
    {
        [ChildActionOnly]
        public ActionResult items()
        {
            return PartialView("_NotificationItems", CoordinatorNotifications.Load(Me));
        }
    }
}
