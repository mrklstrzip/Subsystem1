using System.Web.Mvc;

namespace Subsystem1.Areas.CampusDirector
{
    public class CampusDirectorAreaRegistration : AreaRegistration
    {
        public override string AreaName => "CampusDirector";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "CampusDirector_default",
                "CampusDirector/{controller}/{action}/{id}",
                new { action = "dashboard", id = UrlParameter.Optional }
            );
        }
    }
}