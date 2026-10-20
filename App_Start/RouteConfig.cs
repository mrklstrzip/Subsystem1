using System.Web.Mvc;
using System.Web.Routing;

namespace Subsystem1
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            var route = routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Login", action = "login", id = UrlParameter.Optional },
                namespaces: new[] { "Subsystem1.Areas.Auth.Controllers" }
            );

            route.DataTokens["UseNamespaceFallback"] = false;
            route.DataTokens["area"] = "Auth";
        }
    }
}