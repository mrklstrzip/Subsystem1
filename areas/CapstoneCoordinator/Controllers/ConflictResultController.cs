using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ConflictResultController : CoordinatorControllerBase
    {
        public ActionResult conflictresult(int page = 1)
        {
            CoordinatorData data = CoordinatorData.Load(true);
            data.SetConflictPage(page, 8);
            return View(data);
        }
    }
}
