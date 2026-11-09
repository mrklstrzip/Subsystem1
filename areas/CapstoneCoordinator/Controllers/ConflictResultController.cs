using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ConflictResultController : CoordinatorControllerBase
    {
        public ActionResult conflictresult()
        {
            return View(CoordinatorData.Load(true));
        }
    }
}
