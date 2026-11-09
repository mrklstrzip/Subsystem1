using System.Web.Mvc;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    // "PM Invitations": the Capstone Coordinator validates students as Project Managers by e-mail.
    // The page draws its table from CoordinatorApi/invitations and sends through CoordinatorApi/invite.
    public class InvitationController : CoordinatorControllerBase
    {
        public ActionResult invitations()
        {
            return View();
        }
    }
}
