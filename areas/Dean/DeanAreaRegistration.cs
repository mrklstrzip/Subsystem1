using System.Web.Mvc;

namespace Subsystem1.Areas.Dean
{
    public class DeanAreaRegistration : AreaRegistration
    {
        public override string AreaName => "Dean";

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Dean_default",
                "Dean/{controller}/{action}/{id}",
                new { action = "dashboard", id = UrlParameter.Optional }
            );
        }
    }
}