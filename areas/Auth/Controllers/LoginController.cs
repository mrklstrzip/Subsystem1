using System.Collections.Generic;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;

namespace Subsystem1.Areas.Auth.Controllers
{
    public class LoginController : Controller
    {
  
        private static readonly Dictionary<string, string> RoleSessionKeys =
            new Dictionary<string, string>
            {
                { "admin", "AdminId" },
                { "dean", "DeanId" },
                { "campus-director", "CampusDirectorId" },
                { "capstone-coordinator", "CoordinatorId" },
                { "project-manager", "StudentId" }
            };

        public ActionResult login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult check(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(password))
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            var users = Db.Database.GetCollection<BsonDocument>("users");

            var filter = Builders<BsonDocument>.Filter.Eq(
                "email",
                email.Trim().ToLower()
            );

            var user = users.Find(filter).FirstOrDefault();

            if (user == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            bool passwordOk = false;

            try
            {
                passwordOk = BCrypt.Net.BCrypt.Verify(
                    password,
                    user.GetValue("password", "").ToString()
                );
            }
            catch
            {
                passwordOk = false;
            }

            if (!passwordOk)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

         BsonValue activeFlag;
            if (user.TryGetValue("isActive", out activeFlag) &&
                activeFlag.IsBoolean && !activeFlag.AsBoolean)
            {
                return Json(new
                {
                    success = false,
                    message = "This account is inactive. Please contact the administrator."
                });
            }

            string dbRole = user.GetValue("role", "").ToString();
            string roleKey = Subsystem1.Areas.Admin.Controllers.AdminApiController.RoleKey(dbRole);

            string sessionKey;
            if (!RoleSessionKeys.TryGetValue(roleKey, out sessionKey))
            {
                return Json(new
                {
                    success = false,
                    message = "Your account has no valid role. Please contact the administrator."
                });
            }

            string userId = user["_id"].ToString();

            Session.Clear();

            Session["UserId"] = userId;
            Session["UserRole"] = dbRole;

            Session[sessionKey] = userId;

            string idNumber = user.GetValue("studentId", "").ToString();
            if (idNumber == "") idNumber = user.GetValue("adminId", "").ToString();
            if (idNumber == "") idNumber = user.GetValue("idNumber", "").ToString();

            string program = user.GetValue("program", "").ToString();
            string section = user.GetValue("section", "").ToString();

            return Json(new
            {
                success = true,
                user = new
                {
                    id = idNumber,
                    firstName = user.GetValue("firstName", "").ToString(),
                    lastName = user.GetValue("lastName", "").ToString(),
                    middleName = user.GetValue("middleName", "").ToString(),
                    email = user.GetValue("email", "").ToString(),
                    role = dbRole,
                    program = program,
                    section = section
                }
            });
        }

        [HttpPost]
        public ActionResult logout()
        {

            Session.Clear();
            Session.Abandon();
            return Json(new { success = true });
        }
    }
}