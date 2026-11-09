using System;
using System.Collections.Generic;
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

        // Absolute link to the Sign Up page. App:BaseUrl (Web.config) wins, so the e-mail never
        // depends on the Host header of whoever made the request.
        private string SignupLink(string token)
        {
            string path = Url.Action("signup", "Signup", new { area = "Auth", token = token });
            string baseUrl = Subsystem1.Services.Mailer.BaseUrl;
            if (baseUrl != "") return baseUrl + path;
            return new Uri(Request.Url, path).AbsoluteUri;
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
                List<InviteResult> results = PmInvites.Send(Me, dto.Emails, SignupLink, out fatal);
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
                string error = PmInvites.Resend(Me, dto.Id, SignupLink);
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
