using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class DefenseScheduleController : CoordinatorControllerBase
    {
        public ActionResult defenseschedule(int page = 1)
        {
            CoordinatorData data = CoordinatorData.Load(false);
            data.SetDefensePage(page, 8);
            return View(data);
        }
    }
}
