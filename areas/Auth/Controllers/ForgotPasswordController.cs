using System;
using System.Security.Cryptography;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using Subsystem1.Services;

namespace Subsystem1.Areas.Auth.Controllers
{
    // Forgot password: 1) e-mail a 6-digit code (SMTP)  2) check the code  3) save the new password.
    // The code is stored hashed in the "password_resets" collection (one row per e-mail).
    public class ForgotPasswordController : Controller
    {
        private const int CodeMinutes = 10;
        private const int MaxAttempts = 5;
        private const int ResendSeconds = 30;
        private const int MinPasswordLength = 8;

        private static IMongoCollection<BsonDocument> Users()
        {
            return Db.Database.GetCollection<BsonDocument>("users");
        }

        private static IMongoCollection<BsonDocument> Resets()
        {
            return Db.Database.GetCollection<BsonDocument>("password_resets");
        }

        private static string Norm(string email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        private static string Str(BsonDocument d, string key)
        {
            BsonValue v;
            return d.TryGetValue(key, out v) && !v.IsBsonNull ? v.ToString() : "";
        }

        private JsonResult Fail(string message)
        {
            return Json(new { success = false, message = message });
        }

        private static string NewCode()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var b = new byte[4];
                uint max = uint.MaxValue - (uint.MaxValue % 900000);
                uint n;
                do { rng.GetBytes(b); n = BitConverter.ToUInt32(b, 0); } while (n >= max);
                return (100000 + (n % 900000)).ToString();
            }
        }

        public ActionResult forgotpassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult requestCode(string email)
        {
            try
            {
                email = Norm(email);
                if (email == "" || !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return Fail("Please enter a valid email address.");
                }

                var f = Builders<BsonDocument>.Filter;
                var user = Users().Find(f.Eq("email", email)).FirstOrDefault();
                if (user == null) return Fail("No account found with that email address.");

                BsonValue active;
                if (user.TryGetValue("isActive", out active) && active.IsBoolean && !active.AsBoolean)
                {
                    return Fail("This account is inactive. Please contact the administrator.");
                }

                string problem = Mailer.Problem();
                if (problem != null) return Fail(problem);

                var old = Resets().Find(f.Eq("email", email)).FirstOrDefault();
                if (old != null && old.Contains("createdAt")
                    && (DateTime.UtcNow - old["createdAt"].ToUniversalTime()).TotalSeconds < ResendSeconds)
                {
                    return Fail("Please wait a few seconds before asking for another code.");
                }

                string code = NewCode();
                DateTime now = DateTime.UtcNow;
                Resets().ReplaceOne(
                    f.Eq("email", email),
                    new BsonDocument
                    {
                        { "email", email },
                        { "codeHash", BCrypt.Net.BCrypt.HashPassword(code) },
                        { "createdAt", now },
                        { "expiresAt", now.AddMinutes(CodeMinutes) },
                        { "attempts", 0 }
                    },
                    new UpdateOptions { IsUpsert = true });

                string name = (Str(user, "firstName") + " " + Str(user, "lastName")).Trim();
                string html, text;
                Mailer.ResetCodeBody(name, code, CodeMinutes, out html, out text);

                string error;
                using (var client = Mailer.Connect())
                using (var msg = Mailer.Compose(email, "Your password reset code - QCU CCS Capstone", html, text))
                {
                    error = Mailer.TrySend(client, msg);
                }

                if (error != null)
                {
                    Resets().DeleteOne(f.Eq("email", email));
                    return Fail(error);
                }
                return Json(new { success = true, message = "Code sent." });
            }
            catch (Exception ex)
            {
                return Fail(Problem(ex));
            }
        }

        [HttpPost]
        public ActionResult verifyCode(string email, string code)
        {
            try
            {
                string error = CheckCode(Norm(email), code, false);
                return error == null ? Json(new { success = true }) : Fail(error);
            }
            catch (Exception ex)
            {
                return Fail(Problem(ex));
            }
        }

        [HttpPost]
        public ActionResult resetPassword(string email, string code, string newPassword)
        {
            try
            {
                email = Norm(email);
                newPassword = newPassword ?? "";
                if (newPassword.Length < MinPasswordLength) return Fail("New password must be at least " + MinPasswordLength + " characters long.");
                if (newPassword.Length > 128) return Fail("New password is too long (128 characters max).");

                string error = CheckCode(email, code, true);
                if (error != null) return Fail(error);

                var u = Builders<BsonDocument>.Update;
                var res = Users().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("email", email),
                    u.Combine(u.Set("password", BCrypt.Net.BCrypt.HashPassword(newPassword)), u.Set("updatedAt", DateTime.UtcNow)));
                if (res.MatchedCount == 0) return Fail("No account found with that email address.");

                Resets().DeleteOne(Builders<BsonDocument>.Filter.Eq("email", email));
                return Json(new { success = true, message = "Password successfully reset." });
            }
            catch (Exception ex)
            {
                return Fail(Problem(ex));
            }
        }

        // null = the code is right. Wrong tries are counted and the code is thrown away after MaxAttempts.
        private static string CheckCode(string email, string code, bool consume)
        {
            code = (code ?? "").Trim();
            var f = Builders<BsonDocument>.Filter;
            var row = Resets().Find(f.Eq("email", email)).FirstOrDefault();

            if (row == null) return "That code is not valid. Request a new one.";
            if (DateTime.UtcNow > row["expiresAt"].ToUniversalTime())
            {
                Resets().DeleteOne(f.Eq("email", email));
                return "That code has expired. Request a new one.";
            }

            bool ok = false;
            try { ok = code.Length == 6 && BCrypt.Net.BCrypt.Verify(code, Str(row, "codeHash")); } catch { ok = false; }

            if (!ok)
            {
                int attempts = row.Contains("attempts") ? row["attempts"].ToInt32() : 0;
                attempts++;
                if (attempts >= MaxAttempts)
                {
                    Resets().DeleteOne(f.Eq("email", email));
                    return "Too many incorrect attempts. Request a new code.";
                }
                Resets().UpdateOne(f.Eq("email", email), Builders<BsonDocument>.Update.Set("attempts", attempts));
                return "Incorrect code. " + (MaxAttempts - attempts) + " attempt(s) left.";
            }
            return null;
        }

        private static string Problem(Exception ex)
        {
            bool debug = System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.IsDebuggingEnabled;
            return debug ? "Server error: " + ex.Message : "Something went wrong. Please try again.";
        }
    }
}
