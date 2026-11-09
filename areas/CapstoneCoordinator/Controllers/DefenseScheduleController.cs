using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class DefenseScheduleController : CoordinatorControllerBase
    {
        public ActionResult defenseschedule()
        {
            return View(CoordinatorData.Load(false));
        }
    }
}
