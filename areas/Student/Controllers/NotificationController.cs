using System.Web.Mvc;
using Subsystem1.Areas.Student.Models;

namespace Subsystem1.Areas.Student.Controllers
{
    // Draws the notification drawer's items inside the layout (see _StudentLayout.cshtml),
    // so the shared Notifs.js works on the finished HTML without any change.
    public class NotificationController : StudentControllerBase
    {
        [ChildActionOnly]
        public ActionResult items()
        {
            var store = SubmissionStore.Load(Me);
            return PartialView("_NotificationItems", StudentNotifications.Load(Me, store));
        }
    }
}
