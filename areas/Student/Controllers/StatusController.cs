using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Subsystem1.Areas.Student.ViewModels;

namespace Subsystem1.Areas.Student.Controllers
{
    public class StatusController : Controller
    {
        public ActionResult status()
        {
            var model = new StudentStatusViewModel
            {
                Stage = 5,
                StageLabels = new List<string>
                {
                    "Draft Submitted", "Under Review", "Revisions Complete", "Defense Scheduled", "Defense"
                },
                StageInfos = new List<StageInfo>
                {
                    new StageInfo { Description = "Your manuscript draft was uploaded and received by the capstone coordinator.", Date = new DateTime(2027, 1, 10) },
                    new StageInfo { Description = "Your adviser and panelists are reviewing the manuscript and will send comments.", Date = new DateTime(2027, 1, 17) },
                    new StageInfo { Description = "You addressed the panel's comments and the revised manuscript was accepted.", Date = new DateTime(2027, 1, 31) },
                    new StageInfo { Description = "Your defense schedule, room, and panelists have been set.", Date = new DateTime(2027, 2, 7) },
                    new StageInfo { Description = "Present your work to the panel. Confirm here once your defense is done.", Date = new DateTime(2027, 2, 14) }
                },
                ManuscriptTitle = "SmartQueue: An RFID-Based Queuing and Notification System for University Clinics",
                Panelists = new List<string>
                {
                    "Ms. Ryzza Mae Dizon", "Mr. Eric San Jose", "Mr. Cris Hippers", "Mr. Never Grow Old"
                },
                Room = "IL506",
                DefenseDate = new DateTime(2027, 2, 14)
            };

            model.Activity.Add(new ActivityItem
            {
                Description = "Manuscript changed from \"Draft Submitted\" to \"Under Review\"",
                Date = new DateTime(2027, 2, 14, 10, 0, 0)
            });
            model.Activity.Add(new ActivityItem
            {
                Description = "Manuscript uploaded for review by Maya Santos",
                Date = new DateTime(2027, 2, 14, 13, 0, 0)
            });

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult confirmdefense(bool completed)
        {
            TempData["StatusMessage"] = completed
                ? "Thanks! Your defense has been marked as completed."
                : "Noted. We'll ask again after your scheduled defense.";

            return RedirectToAction("status");
        }
    }
}