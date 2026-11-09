using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.Student.Models;

namespace Subsystem1.Areas.Auth.Controllers
{
    // What the "Request Account" form posts (property names match the form field names).
    public class RequestInput
    {
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string Suffix { get; set; }
        public string Email { get; set; }
        public string IDNumber { get; set; }
        public string Program { get; set; }   // BSIT | BSCS | BSIS
        public string Section { get; set; }   // SBIT-4A ...
        public string Adviser { get; set; }   // users._id of the chosen adviser
    }

    // "Request an Account" (start of the Project Manager flow).
    // Saved in account_requests: { lastName, firstName, middleName, suffix, email, idNumber,
    //   program, section, adviserId, adviserName, status: "Pending", createdAt }
    // The Capstone Adviser and Admin notification drawers are still static pages, so they do not
    // read this collection yet. Each request waits there as "Pending" until they do.
    public class RequestController : Controller
    {
        private static readonly Regex EmailPattern = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
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

        [HttpGet]
        public ActionResult request()
        {
            FillAdvisers();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult request(RequestInput input)
        {
            string error = Save(input);
            if (error == null)
            {
                TempData["Requested"] = true;
                return RedirectToAction("login", "Login", new { area = "Auth" });
            }

            ViewBag.Error = error;
            ViewBag.Values = input;
            FillAdvisers();
            return View();
        }

        // Returns an error message, or null when the request was saved.
        private static string Save(RequestInput i)
        {
            if (i == null) return "Please fill in the form.";

            string last = Regex.Replace(StudentDb.Clean(i.LastName), @"\s+", " ");
            string first = Regex.Replace(StudentDb.Clean(i.FirstName), @"\s+", " ");
            string middle = Regex.Replace(StudentDb.Clean(i.MiddleName), @"\s+", " ");
            string suffix = StudentDb.Clean(i.Suffix);
            string email = StudentDb.Clean(i.Email).ToLowerInvariant();
            string idNumber = StudentDb.Clean(i.IDNumber);
            string program = StudentDb.Clean(i.Program).ToUpperInvariant();
            string section = Regex.Replace(StudentDb.Clean(i.Section), @"\s+", "").ToUpperInvariant();

            if (last == "" || first == "") return "Enter your first and last name.";
            if (last.Length > 60 || first.Length > 60 || middle.Length > 60) return "A name is too long (60 characters max).";
            if (!Suffixes.Contains(suffix)) return "Choose a valid suffix.";
            if (!EmailPattern.IsMatch(email) || email.Length > 120) return "Enter a valid email address.";
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

            // No second account (or second pending request) for the same email or ID number.
            foreach (BsonDocument u in StudentDb.Users().Find(new BsonDocument()).ToList())
            {
                if (string.Equals(StudentDb.Str(u, "email").Trim(), email, StringComparison.OrdinalIgnoreCase))
                {
                    return "An account with that email already exists. Try logging in instead.";
                }
                if (string.Equals(StudentDb.Str(u, "studentId").Trim(), idNumber, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(StudentDb.Str(u, "idNumber").Trim(), idNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return "An account with that ID number already exists. Try logging in instead.";
                }
            }

            var f = Builders<BsonDocument>.Filter;
            var pending = StudentDb.Requests().Find(f.And(
                f.Eq("status", "Pending"),
                f.Or(f.Eq("email", email), f.Eq("idNumber", idNumber)))).FirstOrDefault();
            if (pending != null)
            {
                return "A request with that email or ID number is already waiting for approval.";
            }

            StudentDb.Requests().InsertOne(new BsonDocument
            {
                { "lastName", last },
                { "firstName", first },
                { "middleName", middle },
                { "suffix", suffix },
                { "email", email },
                { "idNumber", idNumber },
                { "program", program },
                { "section", section },
                { "adviserId", StudentDb.DocId(adviser) },
                { "adviserName", StudentDb.PlainName(adviser) },
                { "status", "Pending" },
                { "createdAt", DateTime.UtcNow }
            });
            return null;
        }
    }
}
