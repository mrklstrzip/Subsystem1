using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ScheduleManagementController : CoordinatorControllerBase
    {
        public ActionResult schedulemanagement(int page = 1)
        {
            CoordinatorData data = CoordinatorData.Load(true, onlySubsystem2: true);
            data.SetSchedulePage(page, 8);
            return View(data);
        }
    }
}
