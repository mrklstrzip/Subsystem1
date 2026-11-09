using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ScheduleManagementController : CoordinatorControllerBase
    {
        public ActionResult schedulemanagement()
        {
            return View(CoordinatorData.Load(true));
        }
    }
}
