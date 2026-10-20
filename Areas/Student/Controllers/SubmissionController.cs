using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Subsystem1.Areas.Student.Models;
using Subsystem1.Areas.Student.ViewModels;

namespace Subsystem1.Areas.Student.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly SubmissionStore _store = SubmissionStore.Instance;

        // GET: Student/Submission/submission
        // Preview the three design states with ?demo=empty | inprogress | complete
        public ActionResult submission(string demo)
        {
            if (demo != null) _store.LoadDemo(demo);
            var model = new StudentSubmissionViewModel();
            BuildPage(model);
            return View(model);
        }

        // "Add Capstone Title" pop-up
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddTitle(StudentTitleInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["SubmissionError"] = "Please fill in all required fields.";
                return RedirectToAction("submission");
            }
            _store.Add(input);
            TempData["SuccessMessage"] = "Capstone title submitted.";
            return RedirectToAction("submission");
        }

        // "View Capstone Title" pop-up (loaded with fetch)
        public ActionResult Details(int id)
        {
            var item = _store.Titles.FirstOrDefault(t => t.Id == id);
            if (item == null) return HttpNotFound();
            return PartialView("_TitleDetails", item);
        }

        // DEMO: click a circle. The next pending one moves forward, a completed one goes back.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StepClick(int index)
        {
            var steps = _store.BuildSteps();
            if (index >= 0 && index < steps.Count)
            {
                if (steps[index].Done) _store.RevertTo(index);
                else if (index == steps.FindIndex(x => !x.Done)) _store.AdvanceNext();
            }
            return RedirectToAction("submission");
        }

        // "Is your defense completed?" -> Yes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteDefense()
        {
            _store.SetDefenseComplete();
            return RedirectToAction("submission");
        }

        private void BuildPage(StudentSubmissionViewModel model)
        {

            model.Steps = _store.BuildSteps();
            model.Submissions = _store.Titles.ToList();
            model.Log = _store.Log.OrderByDescending(l => l.Timestamp).ToList();
            model.Programs = SubmissionStore.Programs.ToList();
            model.Advisors = SubmissionStore.Advisors.ToList();
        }
    }
}
