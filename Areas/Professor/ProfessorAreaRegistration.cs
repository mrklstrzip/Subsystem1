using System.Web.Mvc;

namespace Subsystem1.Areas.Professor
{
    public class AdviserAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Professor";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Professor_default",
                "Professor/{controller}/{action}/{id}",
                new { action = "dashboard", id = UrlParameter.Optional }
            );
        }
    }
}