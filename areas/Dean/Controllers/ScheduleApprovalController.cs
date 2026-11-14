using System;
using System.IO;
using System.Net;
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

        // Stream the scheduled manuscript through the authenticated Dean session.
        // The Cloudinary source URL stays server-side and is never exposed to the browser.
        [HttpGet]
        public ActionResult Manuscript(string id)
        {
            try
            {
                DeanManuscriptFile manuscript = DeanData.ManuscriptFile(id);
                if (manuscript == null) return new HttpStatusCodeResult(404, "No PDF manuscript is available for this schedule.");

                Uri current;
                if (!Uri.TryCreate(manuscript.Url, UriKind.Absolute, out current) || !IsCloudinary(current))
                    return new HttpStatusCodeResult(404);

                for (int redirect = 0; redirect <= 3; redirect++)
                {
                    var request = (HttpWebRequest)WebRequest.Create(current);
                    request.Method = "GET";
                    request.Timeout = 30000;
                    request.ReadWriteTimeout = 30000;
                    request.AllowAutoRedirect = false;
                    request.UserAgent = "Subsystem1-Dean-Manuscript-Viewer";

                    HttpWebResponse upstream;
                    try { upstream = (HttpWebResponse)request.GetResponse(); }
                    catch (WebException ex)
                    {
                        var failedResponse = ex.Response as HttpWebResponse;
                        if (failedResponse != null) failedResponse.Dispose();
                        return new HttpStatusCodeResult(502, "Cloudinary could not provide this manuscript.");
                    }

                    using (upstream)
                    {
                        int statusCode = (int)upstream.StatusCode;
                        if (statusCode >= 300 && statusCode < 400)
                        {
                            Uri next;
                            string location = upstream.Headers[HttpResponseHeader.Location];
                            if (redirect == 3 || String.IsNullOrWhiteSpace(location) ||
                                !Uri.TryCreate(current, location, out next) || !IsCloudinary(next))
                                return new HttpStatusCodeResult(502, "Cloudinary returned an invalid manuscript redirect.");
                            current = next;
                            continue;
                        }
                        if (upstream.StatusCode != HttpStatusCode.OK)
                            return new HttpStatusCodeResult(502, "Cloudinary returned an unexpected manuscript response.");

                        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                        Response.Cache.SetNoStore();
                        Response.BufferOutput = false;
                        Response.ContentType = "application/pdf";
                        Response.AddHeader("Content-Disposition", "inline; filename=\"manuscript.pdf\"");
                        Response.AddHeader("X-Content-Type-Options", "nosniff");
                        if (upstream.ContentLength >= 0) Response.AddHeader("Content-Length", upstream.ContentLength.ToString());
                        using (Stream input = upstream.GetResponseStream()) input.CopyTo(Response.OutputStream);
                        return new EmptyResult();
                    }
                }
                return new HttpStatusCodeResult(502);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(502, ErrorText(ex));
            }
        }

        private static bool IsCloudinary(Uri uri)
        {
            return uri != null && uri.Scheme == Uri.UriSchemeHttps &&
                (uri.Host.Equals("api.cloudinary.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase));
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
