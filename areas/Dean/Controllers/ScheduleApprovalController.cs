using Subsystem1.Areas.Dean.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Subsystem1.Areas.Dean.Controllers
{

    
    public class ScheduleApprovalController : Controller
    {
        // GET: Dean/ScheduleApproval
        // TODO: replace this fake data with your DB / stored procedure calls
        private static List<ScheduleListItem> _items = new List<ScheduleListItem>
        {
            new ScheduleListItem { Id = 1, Title = "Lips of An Angel", Section = "BSIT-4A", Program = "BSIT", Author = "Sarah G", Adviser = "Naruto Uzumaki", Date = new DateTime(2027,10,25), Status = "To Approve" },
            new ScheduleListItem { Id = 2, Title = "Huwag na Huwag Mong Sasabihin", Section = "BSCS-4A", Program = "BSCS", Author = "Lea Salonga", Adviser = "Sasuke Uchiha", Date = new DateTime(2027,10,25), Status = "To Approve" },
            new ScheduleListItem { Id = 3, Title = "Pangarap Lang Kita", Section = "BSIS-4A", Program = "BSIS", Author = "Apple D App", Adviser = "Bon Jovie", Date = new DateTime(2027,10,25), Status = "To Approve" },
            new ScheduleListItem { Id = 4, Title = "The Greatest Love of All", Section = "BSCS-4D", Program = "BSCS", Author = "Bamboo", Adviser = "The Beatles", Date = new DateTime(2027,10,25), Status = "Approved" },
        };

        public ActionResult Index()
        {
            ViewBag.Title = "Schedule Approval";
            return View(_items);
        }

        // Called by the pop-up when "View" is clicked
        [HttpGet]
        public ActionResult Details(int id)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item == null) return HttpNotFound();

            // TODO: load the real details from your DB
            var vm = new ScheduleDetailsVM
            {
                Id = item.Id,
                ProjectTitle = "Expertise Based Defense Scheduling",
                DateSubmitted = "Sep 22, 2026",
                Status = item.Status == "Approved" ? "Approved" : "For Approval",
                Student = item.Author,
                ProgramSection = item.Section,
                Adviser = item.Adviser,
                Abstract = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam.",
                Keywords = "scheduling, expertise matching, defense",
                Panelists = new List<PanelistVM>
                {
                    new PanelistVM { Name = "Itadori Yuuji", Expertise = "High" },
                    new PanelistVM { Name = "Remy Butter",   Expertise = "Medium" },
                    new PanelistVM { Name = "Jane Doe",      Expertise = "Low" }
                },
                Time = "8:00AM - 10:00AM",
                DefenseDate = "April 25, 2027",
                Room = "IK601A"
            };

            return Json(vm, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item == null) return Json(new { success = false, message = "Not found." });

            // TODO: update the status in your DB
            item.Status = "Approved";
            return Json(new { success = true });
        }

        public ActionResult ScheduleApprovalView()
        {
            ViewBag.Title = "Schedule Approval";
            return View(_items);
        }

    }
}