using System.Web.Mvc;
using Subsystem1.Areas.Dean.Models;

namespace Subsystem1.Areas.Admin.Controllers
{
    // View-only copy of the Dean Schedule Approval page for the Admin. Signed-in check is done by AdminAuthorize.
    public class ScheduleApprovalController : Controller
    {
        public ActionResult scheduleapproval()
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            return View(DeanData.Approvals());
        }
    }
}
