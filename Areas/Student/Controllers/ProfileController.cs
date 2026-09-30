using Subsystem1.Areas.Student.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Subsystem1.Areas.Student.Controllers
{
    public class ProfileController : Controller
    {
        // GET: Student/Profile
        public ActionResult profile()
        {
            var model = new StudentProfileViewModel();

            model.FullName = "Test Student";
            model.StudentId = "00-0000";
            model.Email = "student@qcu.edu.ph";

            return View(model);
        }
    }
}