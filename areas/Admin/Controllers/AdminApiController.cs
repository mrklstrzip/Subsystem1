using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;

namespace Subsystem1.Areas.Admin.Controllers
{
    public class AdminAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            string area = filterContext.RouteData.DataTokens["area"] as string;
            if (!string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return; // not an Admin page (e.g. the login pages)
            }

            var http = filterContext.HttpContext;
            bool loggedIn = http.Session != null && http.Session["AdminId"] != null;
            bool isApi = string.Equals(
                Convert.ToString(filterContext.RouteData.Values["controller"]),
                "AdminApi",
                StringComparison.OrdinalIgnoreCase);

            if (loggedIn)
            {
                // Block cross-site form posts: browsers cannot add this
                // header to a cross-origin request without a CORS preflight.
                if (isApi
                    && string.Equals(http.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                    && http.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                {
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, message = "Invalid request." },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                return;
            }

            if (isApi)
            {
                // Status 200 on purpose: a 401 would be turned into a login
                // redirect by Forms Authentication and break fetch().
                filterContext.Result = new JsonResult
                {
                    Data = new
                    {
                        success = false,
                        unauthorized = true,
                        message = "Your session has expired. Please log in again."
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                filterContext.Result = new RedirectResult("~/Auth/Login/login");
            }
        }
    }

    // ---------- Request bodies (JSON from admin-app.js) ----------

    public class UserDto
    {
        public string Id { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string Suffix { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }          // UI key, e.g. "project-manager"
        public string IdNumber { get; set; }
        public string Program { get; set; }
        public string Section { get; set; }
        public string AdviserId { get; set; }
        public string Status { get; set; }        // "active" | "inactive" (edit only)
        public string Password { get; set; }      // create only
        public string RequestId { get; set; }     // create only: the account request this account answers
        public List<string> Expertise { get; set; } // create only (faculty)
    }

    public class IdDto
    {
        public string Id { get; set; }
    }

    public class TagDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
    }

    public class FacultyDto
    {
        public string Id { get; set; }
        public List<string> Expertise { get; set; }
    }

    // ------------------------------------------------------------------
    // JSON API used by the four Admin pages:
    //   /Admin/AdminApi/state        (GET)  everything the pages need
    //   /Admin/AdminApi/saveUser     (POST) create or edit a user
    //   /Admin/AdminApi/deleteUser   (POST)
    //   /Admin/AdminApi/saveTag      (POST) create or edit an expertise tag
    //   /Admin/AdminApi/deleteTag    (POST)
    //   /Admin/AdminApi/saveFaculty  (POST) set a faculty member's tags
    //
    // Collections used in Sia_set1_db:
    //   users, expertise_tags (new), admin_activity (new)
    // ------------------------------------------------------------------
    public class AdminApiController : Controller
    {
        private const string UsersName = "users";
        private const string TagsName = "expertise_tags";
        private const string ActivityName = "admin_activity";

        private static readonly string[] SingleRoles =
            { "admin", "campus-director", "capstone-coordinator", "dean" };

        private static readonly Regex EmailPattern =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static IMongoCollection<BsonDocument> Users()
        {
            return Db.Database.GetCollection<BsonDocument>(UsersName);
        }

        private static IMongoCollection<BsonDocument> Requests()
        {
            return Db.Database.GetCollection<BsonDocument>("account_requests");
        }

        private static IMongoCollection<BsonDocument> Tags()
        {
            return Db.Database.GetCollection<BsonDocument>(TagsName);
        }

        private static IMongoCollection<BsonDocument> Activity()
        {
            return Db.Database.GetCollection<BsonDocument>(ActivityName);
        }

        // ---------- role helpers ----------

        // Role text in the database  ->  key used by the UI.
        // Accepts a few spellings so older/hand-made documents still work.
        internal static string RoleKey(string dbRole)
        {
            string r = (dbRole ?? "").Trim().ToLowerInvariant().Replace("-", " ");
            switch (r)
            {
                case "admin": return "admin";
                case "dean": return "dean";
                case "capstone coordinator":
                case "coordinator": return "capstone-coordinator";
                case "campus director":
                case "director": return "campus-director";
                case "professor":
                case "panelist": return "panelist";
                case "adviser":
                case "advisor": return "adviser";
                case "student":
                case "project manager": return "project-manager";
                default: return r.Replace(" ", "-");
            }
        }

        // UI key -> the exact text saved in the database
        // (these match ROLE_HOME in login-check.js / layout-profile.js).
        internal static string RoleDb(string key)
        {
            switch (key)
            {
                case "admin": return "Admin";
                case "dean": return "Dean";
                case "capstone-coordinator": return "Capstone Coordinator";
                case "campus-director": return "Campus Director";
                case "panelist": return "Professor";
                case "adviser": return "Adviser";
                case "project-manager": return "Student";
                default: return null;
            }
        }

        private static string RoleLabel(string key)
        {
            switch (key)
            {
                case "admin": return "Admin";
                case "dean": return "Dean";
                case "capstone-coordinator": return "Capstone Coordinator";
                case "campus-director": return "Campus Director";
                case "panelist": return "Panelist";
                case "adviser": return "Adviser";
                case "project-manager": return "Student";
                default: return key;
            }
        }

        private static bool IsFacultyKey(string key)
        {
            return key == "panelist" || key == "adviser";
        }

        // ---------- document helpers ----------

        private static string Str(BsonDocument d, string name)
        {
            BsonValue v;
            if (d.TryGetValue(name, out v) && v != null && v.IsString)
            {
                return v.AsString;
            }
            return "";
        }

        private static string IdOf(BsonDocument d)
        {
            string[] names = { "idNumber", "studentId", "studentID", "adminId", "employeeId" };
            foreach (string n in names)
            {
                string s = Str(d, n).Trim();
                if (s != "") return s;
            }
            return "";
        }

        private static bool IsActive(BsonDocument d)
        {
            BsonValue v;
            if (d.TryGetValue("isActive", out v) && v != null && v.IsBoolean)
            {
                return v.AsBoolean;
            }
            return true;
        }

        private static List<string> ExpertiseOf(BsonDocument d)
        {
            BsonValue v;
            var list = new List<string>();
            if (d.TryGetValue("expertise", out v) && v != null && v.IsBsonArray)
            {
                foreach (BsonValue item in v.AsBsonArray)
                {
                    if (item.IsString) list.Add(item.AsString);
                }
            }
            return list;
        }

        private static string Clean(string s)
        {
            return (s ?? "").Trim();
        }

        private static string PlainName(BsonDocument d)
        {
            string suf = Str(d, "suffix");
            return (Str(d, "firstName") + " " + Str(d, "lastName") + (suf != "" ? " " + suf : "")).Trim();
        }

        private static string DocId(BsonDocument d)
        {
            return d["_id"].ToString();
        }

        private static object UserRow(BsonDocument d)
        {
            string key = RoleKey(Str(d, "role"));
            string middle = Str(d, "middleName");
            if (middle == "") middle = Str(d, "middlename");

            return new
            {
                id = DocId(d),
                lastName = Str(d, "lastName"),
                firstName = Str(d, "firstName"),
                middleName = middle,
                suffix = Str(d, "suffix"),
                email = Str(d, "email"),
                role = key,
                roleLabel = RoleLabel(key) == key ? Str(d, "role") : RoleLabel(key),
                status = IsActive(d) ? "active" : "inactive",
                idNumber = IdOf(d),
                program = Str(d, "program"),
                section = Str(d, "section"),
                adviserId = Str(d, "adviserId"),
                expertise = ExpertiseOf(d)
            };
        }

        private static object TagRow(BsonDocument d)
        {
            string status = Str(d, "status");
            return new
            {
                id = DocId(d),
                name = Str(d, "name"),
                description = Str(d, "description"),
                status = status == "inactive" ? "inactive" : "active"
            };
        }

        // Skips the empty placeholder document (no email) that is in "users".
        private static List<BsonDocument> RealUsers()
        {
            return Users().Find(new BsonDocument()).ToList()
                .Where(d => Str(d, "email").Trim() != "")
                .ToList();
        }

        private static List<BsonDocument> AllTags()
        {
            return Tags().Find(new BsonDocument()).ToList();
        }

        // Random temporary password: 10 characters with upper case, lower case and a number
        // (no look-alike characters such as 0/O or 1/l/I, so it is easy to read out loud).
        private static string NewTempPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            string all = upper + lower + digits;

            var chars = new List<char>();
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                Func<string, char> pick = set =>
                {
                    var one = new byte[4];
                    rng.GetBytes(one);
                    return set[(int)(BitConverter.ToUInt32(one, 0) % (uint)set.Length)];
                };

                chars.Add(pick(upper));
                chars.Add(pick(lower));
                chars.Add(pick(digits));
                while (chars.Count < 10) chars.Add(pick(all));

                // shuffle (Fisher-Yates) so the first three characters are not always upper/lower/digit
                for (int i = chars.Count - 1; i > 0; i--)
                {
                    var b = new byte[4];
                    rng.GetBytes(b);
                    int j = (int)(BitConverter.ToUInt32(b, 0) % (uint)(i + 1));
                    char tmp = chars[i]; chars[i] = chars[j]; chars[j] = tmp;
                }
            }
            return new string(chars.ToArray());
        }

        private JsonResult Ok(string message)
        {
            return Json(new { success = true, message = message });
        }

        private JsonResult Fail(string message)
        {
            return Json(new { success = false, message = message });
        }

        private static string ErrorText(Exception ex)
        {
            bool debug = System.Web.HttpContext.Current != null
                && System.Web.HttpContext.Current.IsDebuggingEnabled;
            return debug ? "Server error: " + ex.Message : "Something went wrong. Please try again.";
        }

        private JsonResult Error(Exception ex)
        {
            return Fail(ErrorText(ex));
        }

        private void Log(string text, string type, string category)
        {
            try
            {
                Activity().InsertOne(new BsonDocument
                {
                    { "ts", DateTime.UtcNow },
                    { "text", text },
                    { "type", type },         // create | edit | delete
                    { "category", category }, // user | tag
                    { "by", Convert.ToString(Session["AdminId"]) }
                });
            }
            catch
            {
                // Never fail the real action just because logging failed.
            }
        }

        // =========================== READ ===========================

        // Marks a Forwarded account request as Created. It never fails the account creation.
        private static void MarkRequestCreated(string requestId, BsonDocument newUser)
        {
            try
            {
                ObjectId rid;
                if (!ObjectId.TryParse(requestId, out rid)) return;

                var f = Builders<BsonDocument>.Filter;
                var u = Builders<BsonDocument>.Update;
                Requests().UpdateOne(
                    f.And(f.Eq("_id", rid), f.Eq("status", "Forwarded")),
                    u.Combine(
                        u.Set("status", "Created"),
                        u.Set("createdAt", DateTime.UtcNow),
                        u.Set("createdUserId", newUser["_id"].ToString())));
            }
            catch
            {
                // The account was created; the request just stays as it was.
            }
        }

        // One account request the Adviser sent to the Admin (used to pre-fill "Create User").
        [HttpGet]
        public ActionResult requestInfo(string id)
        {
            try
            {
                ObjectId rid;
                if (!ObjectId.TryParse(Clean(id), out rid))
                {
                    return Json(new { success = false, message = "That account request was not found." }, JsonRequestBehavior.AllowGet);
                }

                BsonDocument r = Requests().Find(Builders<BsonDocument>.Filter.Eq("_id", rid)).FirstOrDefault();
                if (r == null)
                {
                    return Json(new { success = false, message = "That account request was not found." }, JsonRequestBehavior.AllowGet);
                }

                string status = Str(r, "status");
                if (status == "Created")
                {
                    return Json(new { success = false, message = "That account was already created." }, JsonRequestBehavior.AllowGet);
                }
                if (status != "Forwarded")
                {
                    return Json(new { success = false, message = "That request is not waiting for the Admin." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    request = new
                    {
                        id = DocId(r),
                        lastName = Str(r, "lastName"),
                        firstName = Str(r, "firstName"),
                        middleName = Str(r, "middleName"),
                        suffix = Str(r, "suffix"),
                        email = Str(r, "email"),
                        idNumber = Str(r, "idNumber"),
                        program = Str(r, "program"),
                        section = Str(r, "section"),
                        adviserId = Str(r, "adviserId")
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Something went wrong. Please try again." }, JsonRequestBehavior.AllowGet);
            }
        }

        private static DateTime ActivityDate(BsonDocument doc, string field)
        {
            BsonValue value;

            if (doc.TryGetValue(field, out value) &&
                value != null &&
                value.IsBsonDateTime)
            {
                return value.ToUniversalTime();
            }

            return DateTime.MinValue;
        }


        private static bool IsTrackedSystemActivity(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string t = text.Trim();
            var c = StringComparison.OrdinalIgnoreCase;

            // ADMIN
            if (t.StartsWith("Admin: ", c))
            {
                t = t.Substring("Admin: ".Length);

                return
                    t.StartsWith("New dean account created:", c) ||
                    t.StartsWith("New campus director account created:", c) ||
                    t.StartsWith("New capstone coordinator account created:", c) ||
                    t.StartsWith("Account updated:", c) ||
                    t.StartsWith("Account activated:", c) ||
                    t.StartsWith("Account deactivated:", c) ||
                    t.StartsWith("New Expertise Tag created:", c) ||
                    t.StartsWith("Expertise Tag modified:", c) ||
                    t.StartsWith("Faculty profile updated:", c);
            }

            // CAMPUS DIRECTOR
            if (t.StartsWith("Campus Director: ", c))
            {
                t = t.Substring("Campus Director: ".Length);

                bool buildingAction =
                    t.StartsWith("Building ", c) &&
                    (t.EndsWith(" added", c) ||
                     t.EndsWith(" details updated", c) ||
                     t.IndexOf(" status changed to ", c) >= 0);

                bool roomAdded =
                    t.StartsWith("Room ", c) &&
                    t.IndexOf(" added to ", c) >= 0;

                return buildingAction || roomAdded;
            }

            // CAPSTONE COORDINATOR
            if (t.StartsWith("Capstone Coordinator: ", c))
            {
                t = t.Substring("Capstone Coordinator: ".Length);

                return
                    t.StartsWith("Panel and schedule generated for", c) ||
                    t.StartsWith("Schedule submitted to the Dean for", c) ||
                    t.StartsWith("Schedule edited for", c);
            }

            // DEAN
            return
                t.StartsWith("Dean approved the defense schedule for", c) ||
                t.StartsWith("Dean rejected the defense schedule for", c);
        }


        [HttpGet]
        public ActionResult state()
        {
            try
            {
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);

                var users = RealUsers().Select(d => UserRow(d)).ToList();

                var tags = AllTags()
                    .OrderBy(t => Str(t, "name"), StringComparer.OrdinalIgnoreCase)
                    .Select(t => TagRow(t))
                    .ToList();

                var adminLogs = Activity()
    .Find(new BsonDocument())
    .Sort(Builders<BsonDocument>.Sort.Descending("ts"))
    .Limit(50)
    .ToList()
    .Select(a => new
    {
        at = ActivityDate(a, "ts"),
        text = "Admin: " + Str(a, "text"),
        type = Str(a, "type"),
        category = Str(a, "category")
    });

                var campusLogs = Db.Database
                    .GetCollection<BsonDocument>("campus_activity")
                    .Find(new BsonDocument())
                    .Sort(Builders<BsonDocument>.Sort.Descending("ts"))
                    .Limit(50)
                    .ToList()
                    .Select(a => new
                    {
                        at = ActivityDate(a, "ts"),
                        text = "Campus Director: " + Str(a, "text"),
                        type = Str(a, "type"),
                        category = Str(a, "category")
                    });

                var coordinatorLogs = Db.Database
                    .GetCollection<BsonDocument>("coordinator_activity")
                    .Find(new BsonDocument())
                    .Sort(Builders<BsonDocument>.Sort.Descending("at"))
                    .Limit(50)
                    .ToList()
                    .Select(a => new
                    {
                        at = ActivityDate(a, "at"),
                        text = Str(a, "text").StartsWith(
                            "Dean approved",
                            StringComparison.OrdinalIgnoreCase)
                            ? Str(a, "text")
                            : "Capstone Coordinator: " + Str(a, "text"),
                        type = Str(a, "text").IndexOf(
                            "generated", StringComparison.OrdinalIgnoreCase) >= 0
                            ? "create" : "edit",
                        category = "schedule"
                    });

                var activity = adminLogs
                    .Concat(campusLogs)
                    .Concat(coordinatorLogs)
                    .Where(a => a.at != DateTime.MinValue &&
                                !string.IsNullOrWhiteSpace(a.text) && 
                                IsTrackedSystemActivity(a.text))
                    .OrderByDescending(a => a.at)
                    .Take(50)
                    .Select(a => new
                    {
                        at = a.at.ToString("o"),
                        text = a.text,
                        type = a.type,
                        category = a.category
                    })
                    .ToList();

                return Json(new { success = true, users = users, tags = tags, activity = activity },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) }, JsonRequestBehavior.AllowGet);
            }
        }

        // ========================== USERS ===========================

        // ========================== MY PROFILE ===========================

        public class AdminPasswordDto
        {
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
        }

        private BsonDocument CurrentAdmin()
        {
            ObjectId oid;
            if (!ObjectId.TryParse(Convert.ToString(Session["AdminId"]), out oid)) return null;
            return Users().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
        }

        [HttpGet]
        public ActionResult me()
        {
            try
            {
                BsonDocument d = CurrentAdmin();
                if (d == null) return Json(new { success = false, unauthorized = true }, JsonRequestBehavior.AllowGet);

                string middle = Str(d, "middleName");
                if (middle == "") middle = Str(d, "middlename");
                string id = Str(d, "adminId");
                if (id == "") id = IdOf(d);

                return Json(new
                {
                    success = true,
                    me = new
                    {
                        id = id,
                        firstName = Str(d, "firstName"),
                        lastName = Str(d, "lastName"),
                        middleName = middle,
                        email = Str(d, "email"),
                        role = "Admin"
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ErrorText(ex) }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult updateProfile(ProfileDto dto)
        {
            try
            {
                BsonDocument d = CurrentAdmin();
                if (d == null) return Json(new { success = false, unauthorized = true });

                string first, middle, last, email;
                string error = ProfileUpdater.Update(d, dto, out first, out middle, out last, out email);
                if (error != null) return Fail(error);

                return Json(new
                {
                    success = true,
                    message = "Profile updated successfully.",
                    me = new { firstName = first, middleName = middle, lastName = last, email = email }
                });
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        [HttpPost]
        public ActionResult changePassword(AdminPasswordDto dto)
        {
            try
            {
                BsonDocument d = CurrentAdmin();
                if (d == null) return Json(new { success = false, unauthorized = true });
                if (dto == null) return Fail("No data received.");

                string current = dto.CurrentPassword ?? "";
                string next = dto.NewPassword ?? "";

                if (current == "" || next == "") return Fail("Please fill in all password fields.");
                if (next.Length < 8) return Fail("New password must be at least 8 characters long.");
                if (next.Length > 128) return Fail("New password is too long (128 characters max).");

                bool currentOk;
                try { currentOk = BCrypt.Net.BCrypt.Verify(current, Str(d, "password")); }
                catch { currentOk = false; }
                if (!currentOk) return Fail("Incorrect current password.");
                if (current == next) return Fail("New password must be different from the current password.");

                var u = Builders<BsonDocument>.Update;
                Users().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", d["_id"]),
                    u.Combine(u.Set("password", BCrypt.Net.BCrypt.HashPassword(next)), u.Set("updatedAt", DateTime.UtcNow)));

                return Ok("Password successfully updated!");
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        [HttpPost]
        public ActionResult saveUser(UserDto dto)
        {
            try
            {
                if (dto == null) return Fail("No data received.");

                string lastName = Clean(dto.LastName);
                string firstName = Clean(dto.FirstName);
                string middleName = Clean(dto.MiddleName);
                string suffix = Clean(dto.Suffix);
                string email = Clean(dto.Email).ToLowerInvariant();
                string roleKey = Clean(dto.Role);
                string idNumber = Clean(dto.IdNumber);
                string program = Clean(dto.Program).ToUpperInvariant();
                string section = Clean(dto.Section);
                string adviserId = Clean(dto.AdviserId);
                string password = dto.Password ?? "";

                if (lastName == "" || firstName == "" || email == "")
                {
                    return Fail("Please fill in all required fields (Name, Email).");
                }
                if (!EmailPattern.IsMatch(email)) return Fail("Please enter a valid email address.");

                string dbRole = RoleDb(roleKey);
                if (dbRole == null || roleKey == "admin") return Fail("Invalid role.");

                bool isStudent = roleKey == "project-manager";
                bool isFaculty = IsFacultyKey(roleKey);

                bool isEdit = Clean(dto.Id) != "";
                
                if (!isEdit &&
    roleKey != "campus-director" &&
    roleKey != "capstone-coordinator" &&
    roleKey != "dean")
                {
                    return Fail(
                        "Only Campus Director, Capstone Coordinator, and Dean accounts can be created."
                    );
                }

                ObjectId oid = ObjectId.Empty;
                BsonDocument existing = null;

                var users = Users();
                var all = RealUsers();

                if (isEdit)
                {
                    if (!ObjectId.TryParse(dto.Id, out oid)) return Fail("Account not found.");
                    existing = users.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                    if (existing == null) return Fail("Account not found.");
                    if (RoleKey(Str(existing, "role")) == "admin")
                    {
                        return Fail("Admin accounts cannot be edited.");
                    }
                }
                string selfId = isEdit ? oid.ToString() : "";

                // ----- uniqueness checks -----
                if (all.Any(d => DocId(d) != selfId
                    && string.Equals(Str(d, "email").Trim(), email, StringComparison.OrdinalIgnoreCase)))
                {
                    return Fail("That email is already used by another account.");
                }
                if (idNumber != "" && all.Any(d => DocId(d) != selfId
                    && string.Equals(IdOf(d), idNumber, StringComparison.OrdinalIgnoreCase)))
                {
                    return Fail("That ID number is already used by another account.");
                }
                if (SingleRoles.Contains(roleKey) && all.Any(d => DocId(d) != selfId
                    && RoleKey(Str(d, "role")) == roleKey))
                {
                    return Fail("There is already a " + RoleLabel(roleKey)
                        + " account. Only one is allowed, so edit or delete the existing one first.");
                }

                // ----- role-specific checks -----
                if (isStudent)
                {
                    if (program != "BSIT" && program != "BSIS" && program != "BSCS")
                    {
                        return Fail("Please select a program for the student.");
                    }
                    if (section == "") return Fail("Please enter the student's section.");

                    if (adviserId != "")
                    {
                        BsonDocument adviser = all.FirstOrDefault(d => DocId(d) == adviserId);
                        if (adviser == null || !IsFacultyKey(RoleKey(Str(adviser, "role"))))
                        {
                            return Fail("The selected adviser does not exist.");
                        }
                    }
                }

                DateTime now = DateTime.UtcNow;

                // =============== CREATE ===============
                if (!isEdit)
                {
                    // The admin no longer types a password: the system generates it.
                    password = NewTempPassword();

                    var doc = new BsonDocument
                    {
                        { "lastName", lastName },
                        { "firstName", firstName },
                        { "middleName", middleName },
                        { "suffix", suffix },
                        { "email", email },
                        { "password", BCrypt.Net.BCrypt.HashPassword(password) },
                        { "role", dbRole },
                        { "idNumber", idNumber },
                        { "isActive", true },
                        { "createdAt", now },
                        { "updatedAt", now }
                    };

                    if (isStudent)
                    {
                        doc["studentId"] = idNumber; // the student login reads this field
                        doc["program"] = program;
                        doc["section"] = section;
                        doc["adviserId"] = adviserId;
                    }

                    if (isFaculty)
                    {
                        var activeNames = new HashSet<string>(
                            AllTags().Where(t => Str(t, "status") != "inactive").Select(t => Str(t, "name")),
                            StringComparer.OrdinalIgnoreCase);

                        var chosen = (dto.Expertise ?? new List<string>())
                            .Select(n => Clean(n))
                            .Where(n => n != "" && activeNames.Contains(n))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        doc["expertise"] = new BsonArray(chosen);
                    }

                    users.InsertOne(doc);

                    // The Adviser sent a "Request an Account" here: this account answers it.
                    if (isStudent && Clean(dto.RequestId) != "")
                    {
                        MarkRequestCreated(Clean(dto.RequestId), doc);
                    }

                    Log("New " + RoleLabel(roleKey).ToLowerInvariant() + " account created: "
                        + PlainName(doc), "create", "user");
                    return Json(new { success = true, message = "Account created.", tempPassword = password });
                }

                // =============== EDIT ===============
                bool newActive = string.Equals(Clean(dto.Status), "inactive", StringComparison.OrdinalIgnoreCase)
                    ? false : true;
                bool oldActive = IsActive(existing);
                bool wasFaculty = IsFacultyKey(RoleKey(Str(existing, "role")));

                var b = Builders<BsonDocument>.Update;
                var changes = new List<UpdateDefinition<BsonDocument>>
                {
                    b.Set("lastName", lastName),
                    b.Set("firstName", firstName),
                    b.Set("middleName", middleName),
                    b.Set("suffix", suffix),
                    b.Set("email", email),
                    b.Set("role", dbRole),
                    b.Set("idNumber", idNumber),
                    b.Set("isActive", newActive),
                    b.Set("updatedAt", now)
                };

                if (isStudent)
                {
                    changes.Add(b.Set("studentId", idNumber));
                    changes.Add(b.Set("program", program));
                    changes.Add(b.Set("section", section));
                    changes.Add(b.Set("adviserId", adviserId));
                }
                else
                {
                    changes.Add(b.Unset("studentId"));
                    changes.Add(b.Unset("program"));
                    changes.Add(b.Unset("section"));
                    changes.Add(b.Unset("adviserId"));
                }

                if (!isFaculty)
                {
                    changes.Add(b.Unset("expertise"));
                }

                users.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", oid), b.Combine(changes));

                if (wasFaculty && !isFaculty)
                {
                    ClearAdviser(oid.ToString());
                }

                string who = firstName + " " + lastName + (suffix != "" ? " " + suffix : "");
                string label = RoleLabel(roleKey);
                if (newActive != oldActive)
                {
                    Log("Account " + (newActive ? "activated" : "deactivated") + ": " + who + " (" + label + ")",
                        newActive ? "create" : "delete", "user");
                }
                else
                {
                    Log("Account updated: " + who + " (" + label + ")", "edit", "user");
                }
                return Ok("Account updated.");
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        [HttpPost]
        public ActionResult deleteUser(IdDto dto)
        {
            try
            {
                ObjectId oid;
                if (dto == null || !ObjectId.TryParse(Clean(dto.Id), out oid))
                {
                    return Fail("Account not found.");
                }

                var users = Users();
                var doc = users.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                if (doc == null) return Fail("Account not found.");

                string key = RoleKey(Str(doc, "role"));
                if (key == "admin") return Fail("Admin accounts cannot be deleted.");
                if (Convert.ToString(Session["AdminId"]) == oid.ToString())
                {
                    return Fail("You cannot delete your own account.");
                }

                users.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", oid));

                if (IsFacultyKey(key)) ClearAdviser(oid.ToString());

                string label = RoleLabel(key) == key ? Str(doc, "role") : RoleLabel(key);
                Log("Account deleted: " + PlainName(doc) + " (" + label + ")", "delete", "user");
                return Ok("Account deleted.");
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        // Students who had this adviser are left without one.
        private static void ClearAdviser(string adviserId)
        {
            Users().UpdateMany(
                Builders<BsonDocument>.Filter.Eq("adviserId", adviserId),
                Builders<BsonDocument>.Update.Set("adviserId", ""));
        }

        // ======================= EXPERTISE TAGS ======================

        [HttpPost]
        public ActionResult saveTag(TagDto dto)
        {
            try
            {
                if (dto == null) return Fail("No data received.");

                string name = Clean(dto.Name);
                string desc = Clean(dto.Description);
                if (name == "" || desc == "") return Fail("Please fill in the tag name and description.");
                if (name.Length > 60) return Fail("Tag name is too long (60 characters max).");
                if (desc.Length > 300) return Fail("Description is too long (300 characters max).");

                var tags = Tags();
                var all = AllTags();

                bool isEdit = Clean(dto.Id) != "";
                ObjectId oid = ObjectId.Empty;
                BsonDocument existing = null;

                if (isEdit)
                {
                    if (!ObjectId.TryParse(dto.Id, out oid)) return Fail("Expertise tag not found.");
                    existing = tags.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                    if (existing == null) return Fail("Expertise tag not found.");
                }
                string selfId = isEdit ? oid.ToString() : "";

                if (all.Any(t => DocId(t) != selfId
                    && string.Equals(Str(t, "name").Trim(), name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Fail("An expertise tag with that name already exists.");
                }

                DateTime now = DateTime.UtcNow;

                if (!isEdit)
                {
                    tags.InsertOne(new BsonDocument
                    {
                        { "name", name },
                        { "description", desc },
                        { "status", "active" },
                        { "createdAt", now },
                        { "updatedAt", now }
                    });
                    Log("New Expertise Tag created: " + name, "create", "tag");
                    return Ok("Expertise tag created.");
                }

                string status = string.Equals(Clean(dto.Status), "inactive", StringComparison.OrdinalIgnoreCase)
                    ? "inactive" : "active";
                string oldName = Str(existing, "name");

                var tu = Builders<BsonDocument>.Update;
                tags.UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    tu.Combine(
                        tu.Set("name", name),
                        tu.Set("description", desc),
                        tu.Set("status", status),
                        tu.Set("updatedAt", now)));

                // Keep faculty profiles in sync with the renamed tag.
                if (oldName != name)
                {
                    var users = Users();
                    var affected = users.Find(Builders<BsonDocument>.Filter.Eq("expertise", oldName)).ToList();
                    foreach (BsonDocument u in affected)
                    {
                        var renamed = ExpertiseOf(u)
                            .Select(n => n == oldName ? name : n)
                            .Distinct()
                            .ToList();
                        users.UpdateOne(
                            Builders<BsonDocument>.Filter.Eq("_id", u["_id"]),
                            Builders<BsonDocument>.Update.Set("expertise", new BsonArray(renamed)));
                    }
                }

                // An inactive tag cannot be assigned, so take it off every profile.
                if (status == "inactive")
                {
                    Users().UpdateMany(
                        Builders<BsonDocument>.Filter.Eq("expertise", name),
                        Builders<BsonDocument>.Update.Pull("expertise", name));
                }

                Log("Expertise Tag modified: " + name, "edit", "tag");
                return Ok("Expertise tag updated.");
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        [HttpPost]
        public ActionResult deleteTag(IdDto dto)
        {
            try
            {
                ObjectId oid;
                if (dto == null || !ObjectId.TryParse(Clean(dto.Id), out oid))
                {
                    return Fail("Expertise tag not found.");
                }

                var tags = Tags();
                var doc = tags.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                if (doc == null) return Fail("Expertise tag not found.");

                string name = Str(doc, "name");

                tags.DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", oid));

                Users().UpdateMany(
                    Builders<BsonDocument>.Filter.Eq("expertise", name),
                    Builders<BsonDocument>.Update.Pull("expertise", name));

                Log("Expertise Tag deleted: " + name, "delete", "tag");
                return Ok("Expertise tag deleted.");
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }

        // ======================= FACULTY PROFILE =====================

        [HttpPost]
        public ActionResult saveFaculty(FacultyDto dto)
        {
            try
            {
                ObjectId oid;
                if (dto == null || !ObjectId.TryParse(Clean(dto.Id), out oid))
                {
                    return Fail("Faculty member not found.");
                }

                var users = Users();
                var doc = users.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                if (doc == null) return Fail("Faculty member not found.");
                if (!IsFacultyKey(RoleKey(Str(doc, "role"))))
                {
                    return Fail("Only panelists and advisers have expertise tags.");
                }

                var activeNames = AllTags()
                    .Where(t => Str(t, "status") != "inactive")
                    .Select(t => Str(t, "name"))
                    .ToList();

                // Only existing, active tags are saved (canonical spelling from the tag list).
                var chosen = new List<string>();
                foreach (string raw in dto.Expertise ?? new List<string>())
                {
                    string match = activeNames.FirstOrDefault(
                        n => string.Equals(n, Clean(raw), StringComparison.OrdinalIgnoreCase));
                    if (match != null && !chosen.Contains(match)) chosen.Add(match);
                }

                var fu = Builders<BsonDocument>.Update;
                users.UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    fu.Combine(
                        fu.Set("expertise", new BsonArray(chosen)),
                        fu.Set("updatedAt", DateTime.UtcNow)));

                Log("Faculty profile updated: " + PlainName(doc), "edit", "user");
                return Ok("Faculty profile updated.");
            }
            catch (Exception ex)
            {
                return Error(ex);
            }
        }
    }
}
