using System;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.Dean.Models;
using static Subsystem1.Data.SharedDb;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DeanPasswordDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class DeanIdDto
    {
        public string Id { get; set; }
    }

    // /Dean/DeanApi/{me, changePassword, notificationRead}
    public class DeanApiController : DeanControllerBase
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
                string id = Str(Me, "idNumber");
                return Json(new
                {
                    success = true,
                    me = new
                    {
                        id = id != "" ? id : IdOf(Me),
                        firstName = Str(Me, "firstName"),
                        lastName = Str(Me, "lastName"),
                        middleName = middle,
                        email = Str(Me, "email"),
                        role = "Dean"
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
        public ActionResult changePassword(DeanPasswordDto dto)
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
        public ActionResult notificationRead(DeanIdDto dto)
        {
            try
            {
                if (dto == null) return Json(new { success = false, message = "No data received." });
                DeanNotifications.MarkRead(DocId(Me), dto.Id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) });
            }
        }
    }
}
