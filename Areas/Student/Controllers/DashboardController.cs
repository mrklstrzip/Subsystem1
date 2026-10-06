using System.Web.Mvc;
using Subsystem1.Areas.Student.ViewModels;

namespace Subsystem1.Areas.Student.Controllers
{
    public class DashboardController : Controller
    {
        public ActionResult dashboard()
        {
             var model = new StudentDashboardViewModel();


                         model.ManuscriptStatus = "Under Review";
            model.Stage = 2;
            model.TotalStages = 5;
            model.StatusDate = DateTime.Today;

            model.DefenseDate = DateTime.Today.AddDays(12);
            model.DefenseVenue = "Room 101";

            model.Panel.Add(new PanelistItem { Name = "Test Panelist", Role = "Panel Chair", Department = "Department of Computer Studies" });
            model.Panel.Add(new PanelistItem { Name = "Test Panelist", Role = "Panel Member", Department = "Department of Information Technology" });

            model.Activity.Add(new ActivityItem { Description = "Manuscript changed from \"Draft Submitted\" to \"Under Review\"", Date = DateTime.Today });
            model.Activity.Add(new ActivityItem { Description = "Manuscript uploaded for review", Date = DateTime.Today.AddDays(-3) });

             return View(model);
        }
    }
}
