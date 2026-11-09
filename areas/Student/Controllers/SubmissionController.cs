using System;
using System.Linq;
using System.Web.Mvc;
using Subsystem1.Areas.Student.Models;
using Subsystem1.Areas.Student.ViewModels;

namespace Subsystem1.Areas.Student.Controllers
{
    public class SubmissionController : StudentControllerBase
    {
        // GET: Student/Submission/submission
        public ActionResult submission()
        {
            var store = SubmissionStore.Load(Me);
            var model = new StudentSubmissionViewModel
            {
                Steps = store.BuildSteps(),
                Submissions = store.Titles.ToList(),
                Log = store.Log.ToList(),
                Programs = SubmissionStore.Programs.ToList(),
                DefaultProgram = StudentDb.ProgramName(StudentDb.Str(Me, "program")),
                DefaultAdviserId = StudentDb.Str(Me, "adviserId"),
                DefaultSection = StudentDb.Str(Me, "section"),
                DefaultDatePublished = StudentDb.NowManila().ToString("yyyy-MM-dd"),
                ProjectManager = StudentDb.PlainName(Me),
                Advisers = StudentDb.Advisers()
                    .Select(a => new AdviserOption { Id = StudentDb.DocId(a), Name = StudentDb.PlainName(a) })
                    .ToList()
            };
            return View(model);
        }

        // "Add Capstone Title" pop-up
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddTitle(StudentTitleInput input)
        {
            if (input == null || !ModelState.IsValid)
            {
                TempData["SubmissionError"] = "Please fill in all required fields.";
                return RedirectToAction("submission");
            }

            string error = SubmissionStore.AddTitle(Me, input);
            if (error != null) TempData["SubmissionError"] = error;
            else TempData["SuccessMessage"] = "Capstone title submitted.";
            return RedirectToAction("submission");
        }

        // "View Capstone Title" pop-up (loaded with fetch). Only the student's own title opens.
        public ActionResult Details(string id)
        {
            var item = SubmissionStore.Load(Me).Titles.FirstOrDefault(t => t.Id == id);
            if (item == null) return HttpNotFound();
            return PartialView("_TitleDetails", item);
        }

        // "Is your defense completed?" -> Yes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteDefense()
        {
            string error = SubmissionStore.CompleteDefense(Me);
            if (error != null) TempData["SubmissionError"] = error;
            return RedirectToAction("submission");
        }
    }
}
