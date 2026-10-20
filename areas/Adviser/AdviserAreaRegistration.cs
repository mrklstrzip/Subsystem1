using System.Web.Mvc;

namespace Subsystem1.Areas.Adviser
{
    public class AdviserAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Adviser";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Adviser_default",
                "Adviser/{controller}/{action}/{id}",
                new { action = "dashboard", id = UrlParameter.Optional }
            );
        }
    }
}