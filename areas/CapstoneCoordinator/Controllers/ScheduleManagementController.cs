using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ScheduleManagementController : CoordinatorControllerBase
    {
        public ActionResult schedulemanagement()
        {
            CoordinatorData data = CoordinatorData.Load(true, onlySubsystem2: true);
            return View(data);
        }
    }
}
