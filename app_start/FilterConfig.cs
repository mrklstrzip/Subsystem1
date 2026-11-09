using System.Web.Mvc;

namespace Subsystem1.App_Start
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());

            // Blocks every Admin page/API call unless an admin is logged in on the server.
            filters.Add(new Subsystem1.Areas.Admin.Controllers.AdminAuthorizeAttribute());

            // Blocks every CampusDirector page/API call unless a campus director is logged in on the server.
            filters.Add(new Subsystem1.Areas.CampusDirector.Controllers.CampusDirectorAuthorizeAttribute());

            // And the Capstone Coordinator area.
            filters.Add(new Subsystem1.Areas.CapstoneCoordinator.Controllers.CoordinatorAuthorizeAttribute());

            // And the Dean area.
            filters.Add(new Subsystem1.Areas.Dean.Controllers.DeanAuthorizeAttribute());

            // Same for the Student (project manager) area.
            filters.Add(new Subsystem1.Areas.Student.Controllers.StudentAuthorizeAttribute());
        }
    }
}