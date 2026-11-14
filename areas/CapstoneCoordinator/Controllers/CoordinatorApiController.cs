using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class CoPasswordDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class CoIdDto
    {
        public string Id { get; set; }
    }

    public class CoInviteDto
    {
        public List<string> Emails { get; set; }
    }

    public class CoSaveDto
    {
        public string Id { get; set; }
        public List<string> Panelists { get; set; }
        public string Date { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public string Room { get; set; }
        public bool Force { get; set; }
    }

    // /CapstoneCoordinator/CoordinatorApi/{me, changePassword, notificationRead, schedule, save, submit,
    //                                      invite, invitations, resendInvite, revokeInvite}
    public class CoordinatorApiController : CoordinatorControllerBase
    {
        protected override bool IsApi { get { return true; } }

        private static string ErrorText(Exception ex)
        {
            bool debug = System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.IsDebuggingEnabled;
            return debug ? "Server error: " + ex.Message : "Something went wrong. Please try again.";
        }

        [HttpGet]
        public ActionResult me()
        {
            try
            {
                string middle = Str(Me, "middleName");
                if (middle == "") middle = Str(Me, "middlename");
                return Json(new
                {
                    success = true,
                    me = new
                    {
                        id = IdOf(Me),
                        firstName = Str(Me, "firstName"),
                        lastName = Str(Me, "lastName"),
                        middleName = middle,
                        email = Str(Me, "email"),
                        role = "Capstone Coordinator"
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) }, JsonRequestBehavior.AllowGet);
            }
        }

        // Edit Profile: name and e-mail (the picture is replaced by the user's initials).
        [HttpPost]
        public ActionResult updateProfile(Subsystem1.Data.ProfileDto dto)
        {
            try
            {
                string first, middle, last, email;
                string error = Subsystem1.Data.ProfileUpdater.Update(Me, dto, out first, out middle, out last, out email);
                if (error != null) return Json(new { success = false, message = error });

                return Json(new
                {
                    success = true,
                    message = "Profile updated successfully.",
                    me = new { firstName = first, middleName = middle, lastName = last, email = email }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        [HttpPost]
        public ActionResult changePassword(CoPasswordDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });

                string current = dto.CurrentPassword ?? "";
                string next = dto.NewPassword ?? "";

                if (current == "" || next == "")
                    return Json(new { success = false, message = "Please fill in all password fields." });
                if (next.Length < 8)
                    return Json(new { success = false, message = "New password must be at least 8 characters long." });
                if (next.Length > 128)
                    return Json(new { success = false, message = "New password is too long (128 characters max)." });

                bool currentOk;
                try { currentOk = BCrypt.Net.BCrypt.Verify(current, Str(Me, "password")); }
                catch { currentOk = false; }
                if (!currentOk) return Json(new { success = false, message = "Incorrect current password." });

                if (current == next)
                    return Json(new { success = false, message = "New password must be different from the current password." });

                var u = Builders<BsonDocument>.Update;
                Users().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", Me["_id"]),
                    u.Combine(u.Set("password", BCrypt.Net.BCrypt.HashPassword(next)), u.Set("updatedAt", DateTime.UtcNow)));

                return Json(new { success = true, message = "Password successfully updated!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        [HttpPost]
        public ActionResult notificationRead(CoIdDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });
                CoordinatorNotifications.MarkRead(DocId(Me), dto.Id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        // One schedule for the pop-ups (generated schedule, under review, defense schedule).
        [HttpGet]
        public ActionResult schedule(string id)
        {
            try
            {
                object detail = CoordinatorData.Detail(id);
                if (detail == null) return Json(new { success = false, message = "Schedule not found." }, JsonRequestBehavior.AllowGet);
                return Json(new { success = true, schedule = detail }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) }, JsonRequestBehavior.AllowGet);
            }
        }

        // Stream authenticated Cloudinary PDFs through this same-origin endpoint so PDF.js
        // does not depend on Cloudinary CORS configuration.
        [HttpGet]
        public ActionResult manuscript(string id)
        {
            Guid versionId;
            BsonDocument title;
            if (Guid.TryParse(id, out versionId))
            {
                // The reader receives subsystem2Id, preserving the exact selected version.
                title = Subsystem2Manuscripts.ForVersion(versionId.ToString("D"));
            }
            else
            {
                // Legacy Subsystem 1 schedules still use their schedule ObjectId as the file key.
                ObjectId scheduleId;
                if (!ObjectId.TryParse(id, out scheduleId)) return new HttpStatusCodeResult(404);
                BsonDocument scheduleDoc = Schedules().Find(Builders<BsonDocument>.Filter.Eq("_id", scheduleId)).FirstOrDefault();
                if (scheduleDoc == null) return new HttpStatusCodeResult(404);
                title = Subsystem2Manuscripts.LatestForSchedule(
                    Str(scheduleDoc, "titleId"), Str(scheduleDoc, "projectManagerId"), Str(scheduleDoc, "title"));
            }
            string url = title == null ? "" : Str(title, "manuscriptUrl");
            string fileName = title == null ? "" : Str(title, "fileName");
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(415, "This submission does not contain a PDF manuscript.");

            Uri source;
            if (!Uri.TryCreate(url, UriKind.Absolute, out source) ||
                source.Scheme != Uri.UriSchemeHttps ||
                !(source.Host.Equals("api.cloudinary.com", StringComparison.OrdinalIgnoreCase) ||
                  source.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase)))
                return new HttpStatusCodeResult(404);

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(source);
                request.Method = "GET";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.UserAgent = "Subsystem1-Manuscript-Viewer";

                using (var upstream = (HttpWebResponse)request.GetResponse())
                using (Stream input = upstream.GetResponseStream())
                {
                    Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                    Response.Cache.SetNoStore();
                    Response.BufferOutput = false;
                    Response.ContentType = "application/pdf";
                    Response.AddHeader("Content-Disposition", "inline; filename=manuscript.pdf");
                    Response.AddHeader("X-Content-Type-Options", "nosniff");
                    if (upstream.ContentLength >= 0)
                        Response.AddHeader("Content-Length", upstream.ContentLength.ToString());
                    input.CopyTo(Response.OutputStream);
                    return new EmptyResult();
                }
            }
            catch (WebException)
            {
                return new HttpStatusCodeResult(502, "Cloudinary could not provide this manuscript.");
            }
        }

        // Edit panelists / date / time / room. Class overlaps come back as needsConfirm until Force is sent.
        [HttpPost]
        public ActionResult save(CoSaveDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });

                var soft = new List<string>();
                string error = ScheduleGenerator.Save(dto.Id, new PlanInput
                {
                    Panelists = dto.Panelists, Date = dto.Date, Start = dto.Start, End = dto.End, Room = dto.Room
                }, dto.Force, soft);

                if (error != null) return Json(new { success = false, message = error });
                if (soft.Count > 0) return Json(new { success = false, needsConfirm = true, conflicts = soft });

                return Json(new { success = true, message = "Schedule saved.", schedule = CoordinatorData.Detail(dto.Id) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        // Submit to the Dean (an open review is marked Resolved).
        [HttpPost]
        public ActionResult submit(CoIdDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });

                string error = ScheduleGenerator.Submit(dto.Id, PlainName(Me));
                if (error != null) return Json(new { success = false, message = error });
                return Json(new { success = true, message = "Schedule submitted to the Dean." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        // ---------------- Project Manager invitations ----------------

        private string StudentSignupEndpoint()
        {
            string configured;
            Uri currentUrl = Request == null ? null : Request.Url;
            bool runningLocally = currentUrl != null &&
                (currentUrl.IsLoopback || currentUrl.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
            if (runningLocally)
            {
                string localSignupEndpoint = null;
                configured = Environment.GetEnvironmentVariable("SUBSYSTEM2_LOCAL_STUDENT_SIGNUP_URL");
                if (!String.IsNullOrWhiteSpace(configured))
                {
                    configured = ValidateStudentSignupEndpoint(configured);
                    if (IsLocalStudentSignupRunning(configured)) return configured;
                    localSignupEndpoint = configured;
                }

                string[] localEndpoints =
                {
                    "https://localhost:44346/Account/StudentSignup",
                    "http://localhost:5068/Account/StudentSignup",
                    "http://localhost:51978/Account/StudentSignup"
                };
                foreach (string localEndpoint in localEndpoints)
                    if (IsLocalStudentSignupRunning(localEndpoint)) return localEndpoint;

                // Local invitations must stay in the local environment even if Subsystem 2
                // is still starting. Otherwise a local invite token can be sent to Vercel.
                return localSignupEndpoint ?? "https://localhost:44346/Account/StudentSignup";
            }

            configured = Environment.GetEnvironmentVariable("SUBSYSTEM2_STUDENT_SIGNUP_URL");
            if (!String.IsNullOrWhiteSpace(configured))
                return ValidateStudentSignupEndpoint(configured);

            return "https://subsystem2.vercel.app/Account/StudentSignup";
        }

        private static string ValidateStudentSignupEndpoint(string configured)
        {
            Uri signupUri;
            if (!Uri.TryCreate(configured, UriKind.Absolute, out signupUri) ||
                !(signupUri.Scheme == Uri.UriSchemeHttps || Uri.UriSchemeHttp.Equals(signupUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                    (signupUri.IsLoopback || signupUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("Subsystem 2 student signup URL is not configured safely.");
            return signupUri.AbsoluteUri;
        }

        private static bool IsLocalStudentSignupRunning(string endpoint)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "GET";
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;
                request.AllowAutoRedirect = false;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                        return false;
                    string body = reader.ReadToEnd();
                    return body.IndexOf("student-signup-card", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (WebException) { return false; }
            catch (IOException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static string SignupLink(string token, string endpoint)
        {
            var builder = new UriBuilder(new Uri(endpoint, UriKind.Absolute));
            builder.Query = "token=" + Uri.EscapeDataString(token ?? "");
            return builder.Uri.AbsoluteUri;
        }

        [HttpGet]
        public ActionResult invitations()
        {
            try
            {
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                return Json(new { success = true, invitations = PmInvites.ListAll() }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) }, JsonRequestBehavior.AllowGet);
            }
        }

        // Validates each student by e-mail: they get a link to the Sign Up page as a Project Manager.
        [HttpPost]
        public ActionResult invite(CoInviteDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });

                string fatal;
                string signupEndpoint = StudentSignupEndpoint();
                List<InviteResult> results = PmInvites.Send(Me, dto.Emails,
                    token => SignupLink(token, signupEndpoint), out fatal);
                if (fatal != null) return Json(new { success = false, message = fatal });

                int sent = 0, skipped = 0, failed = 0;
                foreach (InviteResult r in results)
                {
                    if (r.Status == "sent" || r.Status == "resent") sent++;
                    else if (r.Status == "failed") failed++;
                    else skipped++;
                }

                return Json(new
                {
                    success = sent > 0,
                    message = sent > 0
                        ? "Sent " + sent + " invitation" + (sent == 1 ? "" : "s") + "."
                        : (failed > 0 ? "The invitations could not be sent." : "No invitations were sent."),
                    sent = sent,
                    skipped = skipped,
                    failed = failed,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        [HttpPost]
        public ActionResult resendInvite(CoIdDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });
                string signupEndpoint = StudentSignupEndpoint();
                string error = PmInvites.Resend(Me, dto.Id,
                    token => SignupLink(token, signupEndpoint));
                if (error != null) return Json(new { success = false, message = error });
                return Json(new { success = true, message = "Invitation sent again." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        [HttpPost]
        public ActionResult revokeInvite(CoIdDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });
                string error = PmInvites.Revoke(dto.Id);
                if (error != null) return Json(new { success = false, message = error });
                return Json(new { success = true, message = "Invitation cancelled." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }
    }
}
