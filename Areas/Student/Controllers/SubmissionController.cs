using Subsystem1.Areas.Student.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;

namespace Subsystem1.Areas.Student.Controllers
{
    public class SubmissionController : Controller
    {
        // GET: Student/Submission/submission
        public ActionResult submission()
        {
            var model = new StudentSubmissionViewModel();
            Populate(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddManuscript(StudentSubmissionViewModel model)
        {
            if (model.ManuscriptFile != null && model.ManuscriptFile.ContentLength > 0)
            {
                var ext = Path.GetExtension(model.ManuscriptFile.FileName).ToLowerInvariant();
                if (ext != ".pdf" && ext != ".doc" && ext != ".docx")
                    ModelState.AddModelError("ManuscriptFile", "Only PDF or Word files are allowed.");
            }

            if (ModelState.IsValid)
            {
                var folder = Server.MapPath("~/App_Data/Manuscripts");
                Directory.CreateDirectory(folder);
                var savedName = Guid.NewGuid() + Path.GetExtension(model.ManuscriptFile.FileName);
                model.ManuscriptFile.SaveAs(Path.Combine(folder, savedName));

                // TODO: Save title, authors, professor, program, date submitted, savedName, etc. to database

                TempData["SuccessMessage"] = "Manuscript submitted successfully.";
                return RedirectToAction("submission");
            }

            Populate(model);
            return View("submission", model);
        }

        private void Populate(StudentSubmissionViewModel model)
        {
            model.DateSubmitted = DateTime.Today;

            model.Submissions = new List<ManuscriptItemDto>
            {
                new ManuscriptItemDto { Id = 1, Title = "SmartQueue: An RFID-Based Queuing and Notification System for University Clinics", DateAndSubmissionTime = new DateTime(2026, 9, 10, 14, 30, 0), Status = "Submitted" },
                new ManuscriptItemDto { Id = 2, Title = "Lorem ipsum: lorem ipsum dolor sit amet consectetur adipiscing elit aute voluptas et", DateAndSubmissionTime = new DateTime(2026, 9, 11, 16, 0, 0), Status = "Processing" },
                new ManuscriptItemDto { Id = 3, Title = "Lorem ipsum: lorem ipsum dolor sit amet consectetur adipiscing elit aute voluptas et", DateAndSubmissionTime = new DateTime(2026, 9, 12, 13, 10, 0), Status = "Completed" }
            };
        }
    }
}