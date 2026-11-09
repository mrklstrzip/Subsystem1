using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using Subsystem1.Areas.Student.Models;

namespace Subsystem1.Areas.Auth.Controllers
{
    // What the Sign Up form posts (property names match the form field names).
    public class SignupInput
    {
        public string Token { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string Suffix { get; set; }
        public string IDNumber { get; set; }
        public string Program { get; set; }   // BSIT | BSCS | BSIS
        public string Section { get; set; }   // SBIT-4A ...
        public string Adviser { get; set; }   // users._id of the chosen adviser
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

    // Sign Up for a student the Capstone Coordinator validated as a Project Manager.
    // The e-mail link is /Auth/Signup/signup?token=...  (see PmInvites). The e-mail address comes from
    // the invitation, not from the form, so only the invited address can be used.
    // The account is saved in users exactly like the Admin's "Create User" (role "Student").
    public class SignupController : Controller
    {
        private static readonly string[] Suffixes = { "", "Jr.", "Sr.", "II", "III", "IV", "V" };
        private static readonly Dictionary<string, string> SectionPrefix = new Dictionary<string, string>
        {
            { "BSIT", "SBIT-" }, { "BSCS", "SBCS-" }, { "BSIS", "SBIS-" }
        };

        private void FillAdvisers()
        {
            ViewBag.Advisers = StudentDb.Advisers()
                .Select(a => new KeyValuePair<string, string>(StudentDb.DocId(a), StudentDb.PlainName(a)))
                .ToList();
        }

        // The link holds a secret: keep it out of caches and out of the Referer header.
        private void Protect()
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.AddHeader("Referrer-Policy", "no-referrer");
        }

        [HttpGet]
        public ActionResult signup(string token)
        {
            Protect();

            string problem;
            BsonDocument inv = PmInvites.Lookup(token, out problem);
            if (inv == null)
            {
                ViewBag.Invalid = problem;
                return View();
            }

            ViewBag.Token = token;
            ViewBag.InviteEmail = StudentDb.Str(inv, "email");
            FillAdvisers();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult signup(SignupInput input)
        {
            Protect();

            string token = input == null ? "" : (input.Token ?? "");
            string problem;
            BsonDocument inv = PmInvites.Lookup(token, out problem);
            if (inv == null)
            {
                ViewBag.Invalid = problem;
                return View();
            }

            string error = Save(input, inv);
            if (error == null)
            {
                TempData["SignedUp"] = true;
                return RedirectToAction("login", "Login", new { area = "Auth" });
            }

            ViewBag.Error = error;
            ViewBag.Values = input;
            ViewBag.Token = token;
            ViewBag.InviteEmail = StudentDb.Str(inv, "email");
            FillAdvisers();
            return View();
        }

        // Returns an error message, or null when the account was created.
        private static string Save(SignupInput i, BsonDocument invite)
        {
            if (i == null) return "Please fill in the form.";

            string last = Regex.Replace(StudentDb.Clean(i.LastName), @"\s+", " ");
            string first = Regex.Replace(StudentDb.Clean(i.FirstName), @"\s+", " ");
            string middle = Regex.Replace(StudentDb.Clean(i.MiddleName), @"\s+", " ");
            string suffix = StudentDb.Clean(i.Suffix);
            string email = StudentDb.Str(invite, "email").Trim().ToLowerInvariant();
            string idNumber = StudentDb.Clean(i.IDNumber);
            string program = StudentDb.Clean(i.Program).ToUpperInvariant();
            string section = Regex.Replace(StudentDb.Clean(i.Section), @"\s+", "").ToUpperInvariant();
            string password = i.Password ?? "";

            if (last == "" || first == "") return "Enter your first and last name.";
            if (last.Length > 60 || first.Length > 60 || middle.Length > 60) return "A name is too long (60 characters max).";
            if (!Suffixes.Contains(suffix)) return "Choose a valid suffix.";
            if (idNumber == "" || idNumber.Length > 30) return "Enter your ID number.";

            string prefix;
            if (!SectionPrefix.TryGetValue(program, out prefix)) return "Choose your program.";
            if (!Regex.IsMatch(section, "^" + Regex.Escape(prefix) + "[A-Z0-9]{1,6}$"))
            {
                return "Enter your section, for example " + prefix + "4A.";
            }

            // The adviser must be a real, active faculty account.
            BsonDocument adviser = null;
            ObjectId? aid = StudentDb.ParseId(i.Adviser);
            if (aid.HasValue)
            {
                adviser = StudentDb.Users().Find(Builders<BsonDocument>.Filter.Eq("_id", aid.Value)).FirstOrDefault();
            }
            if (adviser == null || !StudentDb.IsActive(adviser) || !StudentDb.IsFaculty(adviser))
            {
                return "Choose your adviser.";
            }

            if (password.Length < 8) return "Your password must be at least 8 characters.";
            if (password.Length > 128) return "Your password is too long (128 characters max).";
            if (!Regex.IsMatch(password, "[a-z]") || !Regex.IsMatch(password, "[A-Z]") || !Regex.IsMatch(password, @"\d"))
            {
                return "Your password needs upper and lower case letters and a number.";
            }
            if (password != (i.ConfirmPassword ?? "")) return "The passwords do not match.";

            // No second account for the same email or ID number.
            var f = Builders<BsonDocument>.Filter;
            var exact = new BsonRegularExpression("^" + Regex.Escape(email) + "$", "i");
            if (StudentDb.Users().Find(f.Regex("email", exact)).Any())
            {
                return "An account with that email already exists. Try logging in instead.";
            }
            var idExact = new BsonRegularExpression("^" + Regex.Escape(idNumber) + "$", "i");
            if (StudentDb.Users().Find(f.Or(f.Regex("studentId", idExact), f.Regex("idNumber", idExact))).Any())
            {
                return "An account with that ID number already exists. Try logging in instead.";
            }

            // Use the invitation up first: if two people open the link at once, only one gets through.
            ObjectId inviteId = invite["_id"].AsObjectId;
            if (!PmInvites.Claim(inviteId))
            {
                return "This sign-up link was already used or has expired. Ask the Capstone Coordinator for a new one.";
            }

            DateTime now = DateTime.UtcNow;
            var doc = new BsonDocument
            {
                { "lastName", last },
                { "firstName", first },
                { "middleName", middle },
                { "suffix", suffix },
                { "email", email },
                { "password", BCrypt.Net.BCrypt.HashPassword(password) },
                { "role", "Student" },
                { "idNumber", idNumber },
                { "studentId", idNumber },     // the student login reads this field
                { "program", program },
                { "section", section },
                { "adviserId", StudentDb.DocId(adviser) },
                { "isActive", true },
                { "signupSource", "coordinator-invite" },
                { "invitedById", StudentDb.Str(invite, "invitedById") },
                { "createdAt", now },
                { "updatedAt", now }
            };

            try
            {
                StudentDb.Users().InsertOne(doc);
            }
            catch
            {
                PmInvites.Release(inviteId);    // nothing was created, so the link stays usable
                return "Something went wrong. Please try again.";
            }

            try { PmInvites.SetAcceptedUser(inviteId, StudentDb.DocId(doc)); } catch { /* the account exists; this is only a note */ }
            CoDb.Log(StudentDb.PlainName(doc) + " signed up as Project Manager (" + section + ").");
            return null;
        }
    }
}
