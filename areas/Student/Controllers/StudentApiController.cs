using System;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.Student.Models;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.Student.Controllers
{
    public class PasswordDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class ReadDto
    {
        public string Id { get; set; }
    }

    // ------------------------------------------------------------------
    // JSON API used by the Student profile page and notification drawer:
    //   /Student/StudentApi/me                 (GET)
    //   /Student/StudentApi/changePassword     (POST)
    //   /Student/StudentApi/notificationRead   (POST)
    // ------------------------------------------------------------------
    public class StudentApiController : StudentControllerBase
    {
        protected override bool IsApi { get { return true; } }

        private static string ErrorText(Exception ex)
        {
            bool debug = System.Web.HttpContext.Current != null
                && System.Web.HttpContext.Current.IsDebuggingEnabled;
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
                        role = "Student",
                        program = Str(Me, "program"),
                        section = Str(Me, "section")
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
        public ActionResult changePassword(PasswordDto dto)
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
                try
                {
                    currentOk = BCrypt.Net.BCrypt.Verify(current, Str(Me, "password"));
                }
                catch
                {
                    currentOk = false;
                }
                if (!currentOk) return Json(new { success = false, message = "Incorrect current password." });

                if (current == next)
                    return Json(new { success = false, message = "New password must be different from the current password." });

                var u = Builders<BsonDocument>.Update;
                Users().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", Me["_id"]),
                    u.Combine(
                        u.Set("password", BCrypt.Net.BCrypt.HashPassword(next)),
                        u.Set("updatedAt", DateTime.UtcNow)));

                return Json(new { success = true, message = "Password successfully updated!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }

        [HttpPost]
        public ActionResult notificationRead(ReadDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });
                StudentNotifications.MarkRead(DocId(Me), dto.Id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }
    }
}
