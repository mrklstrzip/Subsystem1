using Subsystem1.Areas.Dean.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class NotificationController : Controller
    {
        [ChildActionOnly]
        public ActionResult Panel()
        {
            // TODO: replace with your DB query
            var vm = new NotificationPanelViewModel
            {
                Items = new List<NotificationItem>
                {
                    new NotificationItem { Id=1, Title="Cho Miyeon submitted a defense schedule", Message="Defense schedule for BSIT-4A, with a title \"Lips of an Angel\"", TimeAgo="5 min ago", IsUnread=true,  Today=true },
                    new NotificationItem { Id=2, Title="Cho Miyeon submitted a defense schedule", Message="Defense schedule for BSCS-4A, with a title \"Lips of an Angel\"", TimeAgo="5 min ago", IsUnread=false, Today=false }
                }
            };
            return PartialView("_NotifPanel", vm);
        }
    }
}