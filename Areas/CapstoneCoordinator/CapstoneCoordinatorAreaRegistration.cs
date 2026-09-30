using System.Web.Mvc;

namespace Subsystem1.Areas.CapstoneCoordinator
{
    public class CapstoneCoordinatorAreaRegistration : AreaRegistration
    {
        public override string AreaName => "CapstoneCoordinator";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "CapstoneCoordinator_default",
                "CapstoneCoordinator/{controller}/{action}/{id}",
                new { action = "dashboard", id = UrlParameter.Optional }
            );
        }
    }
}