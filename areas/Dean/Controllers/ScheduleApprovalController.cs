using System;
using System.Web.Mvc;
using Subsystem1.Areas.Dean.Models;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class ScheduleApprovalController : DeanControllerBase
    {
        public ActionResult Index()
        {
            return RedirectToAction("ScheduleApprovalView");
        }

        public ActionResult ScheduleApprovalView()
        {
            ViewBag.Title = "Schedule Approval";
            return View(DeanData.Approvals());
        }

        // Called by the pop-up when "View" is clicked
        [HttpGet]
        public ActionResult Details(string id)
        {
            try
            {
                ScheduleDetailsVM vm = DeanData.Detail(id);
                if (vm == null) return Json(new { success = false, message = "Schedule not found." }, JsonRequestBehavior.AllowGet);
                return Json(vm, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(string id)
        {
            try
            {
                string error = DeanData.Approve(id, Me);
                if (error != null) return Json(new { success = false, message = error });
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        private static string ErrorText(Exception ex)
        {
            bool debug = System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.IsDebuggingEnabled;
            return debug ? "Server error: " + ex.Message : "Something went wrong. Please try again.";
        }
    }
}
