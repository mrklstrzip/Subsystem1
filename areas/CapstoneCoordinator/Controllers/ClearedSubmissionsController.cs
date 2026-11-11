using System.Web.Mvc;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class ClearedSubmissionsController : CoordinatorControllerBase
    {
        public ActionResult Index()
        {
            return View(Subsystem3Clearances.ReadCleared());
        }
    }
}
