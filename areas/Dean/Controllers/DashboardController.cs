using Subsystem1.Areas.Dean.Models;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DashboardController : Controller
    {
        public ActionResult dashboard()
        {
            // TODO: replace with your DB queries
            var vm = new DashboardViewModel
            {
                TotalDefenses = 24,
                ToApprove = new List<DashboardRow>
                {
                    new DashboardRow { Id=1, Title="Manuscript Title", Section="BSIT-4A", Author="Sarah G",     Adviser="Naruto Uzumaki", Date="10-25-27" },
                    new DashboardRow { Id=2, Title="Manuscript Title", Section="BSCS-4A", Author="Lea Salonga", Adviser="Sasuke Uchiha",  Date="10-25-27" },
                    new DashboardRow { Id=3, Title="Manuscript Title", Section="BSIS-4A", Author="Apple D App", Adviser="Bon Jovie",       Date="10-25-27" }
                },
                Upcoming = new List<DashboardRow>
                {
                    new DashboardRow { Title="Manuscript Title", Section="BSIT-4A", Author="Sarah G",     Adviser="Naruto Uzumaki", Room="IK506", Date="10-25-27", Time="8AM - 10AM" },
                    new DashboardRow { Title="Manuscript Title", Section="BSCS-4A", Author="Lea Salonga", Adviser="Sasuke Uchiha",  Room="IK506", Date="10-25-27", Time="11AM - 1PM" },
                    new DashboardRow { Title="Manuscript Title", Section="BSIS-4A", Author="Apple D App", Adviser="Bon Jovie",       Room="IK506", Date="10-25-27", Time="2PM - 4PM" }
                },
                RecentActivities = new List<ActivityRow>()
            };

            ViewBag.Title = "Dashboard";
            return View(vm);
        }
    }
}