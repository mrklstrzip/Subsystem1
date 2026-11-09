using Subsystem1.Areas.Student.Models;
using Subsystem1.Areas.Student.ViewModels;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Subsystem1.Areas.Student.Controllers
{
    public class DashboardController : StudentControllerBase
    {
        public ActionResult dashboard()
        {
            // The Dashboard reads the same data as the Submission page, so the two always match.
            var store = SubmissionStore.Load(Me);
            var steps = store.BuildSteps();
            int last = steps.FindLastIndex(x => x.Done);      // -1 = nothing submitted yet

            var model = new StudentDashboardViewModel
            {
                UserRole = "Project Manager",
                FirstName = StudentDb.Str(Me, "firstName"),
                TotalStages = steps.Count,
                DefenseDate = store.DefenseDate,
                DefenseVenue = store.Room
            };

            if (last >= 0)
            {
                model.ManuscriptStatus = steps[last].Label;   // e.g. "Defense Scheduled"
                model.Stage = last + 1;                       // e.g. Stage 5 of 6
                model.StatusDate = store.LastChanged;
            }

            foreach (var p in store.Panelists)
            {
                model.Panel.Add(new PanelistItem
                {
                    Name = p.Name,
                    Role = p.Role,
                    Department = p.Department,
                    PhotoUrl = string.IsNullOrEmpty(p.PhotoUrl) ? null : Url.Content(p.PhotoUrl)
                });
            }

            foreach (var l in store.Log.Take(5))
            {
                model.Activity.Add(new ActivityItem { Description = l.Text, Date = l.Timestamp });
            }

            return View(model);
        }
    }
}
