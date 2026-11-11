using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using AdminApi = Subsystem1.Areas.Admin.Controllers.AdminApiController;

namespace Subsystem1.Areas.CampusDirector.Controllers
{
    public class CampusDirectorAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            string area = filterContext.RouteData.DataTokens["area"] as string;
            if (!string.Equals(area, "CampusDirector", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var http = filterContext.HttpContext;
            bool loggedIn = http.Session != null && http.Session["CampusDirectorId"] != null;
            bool isApi = string.Equals(
                Convert.ToString(filterContext.RouteData.Values["controller"]),
                "CampusDirectorApi",
                StringComparison.OrdinalIgnoreCase);

            if (loggedIn)
            {
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

    // ---------- Request bodies (JSON from campusdirector-app.js) ----------

    public class BuildingDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Status { get; set; }   // "Active" | "Unavailable" (edit only)
    }

    public class RoomDto
    {
        public string Id { get; set; }
        public string BuildingId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }   // "Active" | "Unavailable" (edit only)
    }

    public class CdIdDto
    {
        public string Id { get; set; }
    }

    public class PasswordDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
    public class CampusDirectorApiController : Controller
    {
        private const string UsersName = "users";
        private const string BuildingsName = "buildings";
        private const string RoomsName = "rooms";
        private const string ActivityName = "campus_activity";
        private const string SchedulesName = "defense_schedules";

        private static readonly Regex CodePattern =
            new Regex(@"^[A-Za-z0-9\-]{1,10}$", RegexOptions.Compiled);

        private static IMongoCollection<BsonDocument> Users()
        {
            return Db.Database.GetCollection<BsonDocument>(UsersName);
        }

        private static IMongoCollection<BsonDocument> Buildings()
        {
            return Db.Database.GetCollection<BsonDocument>(BuildingsName);
        }

        private static IMongoCollection<BsonDocument> Rooms()
        {
            return Db.Database.GetCollection<BsonDocument>(RoomsName);
        }

        private static IMongoCollection<BsonDocument> Activity()
        {
            return Db.Database.GetCollection<BsonDocument>(ActivityName);
        }

        private static IMongoCollection<BsonDocument> Schedules()
        {
            return Db.Database.GetCollection<BsonDocument>(SchedulesName);
        }

        // ---------- small helpers ----------

        // The Philippines has no daylight saving time, so UTC+8 is always right,
        // whatever time zone the web server itself is set to.
        private static DateTime Manila()
        {
            return DateTime.UtcNow.AddHours(8);
        }

        private static string Str(BsonDocument d, string name)
        {
            BsonValue v;
            if (d.TryGetValue(name, out v) && v != null && v.IsString)
            {
                return v.AsString;
            }
            return "";
        }

        // Text for a field that may be stored as text, a number or an ObjectId.
        private static string AnyStr(BsonDocument d, string name)
        {
            BsonValue v;
            if (!d.TryGetValue(name, out v) || v == null || v.IsBsonNull) return "";
            if (v.IsString) return v.AsString;
            if (v.IsObjectId) return v.AsObjectId.ToString();
            if (v.IsInt32) return v.AsInt32.ToString(CultureInfo.InvariantCulture);
            if (v.IsInt64) return v.AsInt64.ToString(CultureInfo.InvariantCulture);
            if (v.IsDouble) return v.AsDouble.ToString("0.##", CultureInfo.InvariantCulture);
            return v.ToString();
        }

        // Minutes after midnight, from a number or from "09:00" / "9:00 AM" text.
        private static int? Minutes(BsonDocument d, string name)
        {
            BsonValue v;
            if (!d.TryGetValue(name, out v) || v == null) return null;
            if (v.IsInt32) return v.AsInt32;
            if (v.IsInt64) return (int)v.AsInt64;
            if (v.IsDouble) return (int)v.AsDouble;
            if (v.IsString)
            {
                DateTime t;
                string[] formats = { "H:mm", "HH:mm", "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt", "h tt", "htt" };
                if (DateTime.TryParseExact(v.AsString.Trim(), formats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                {
                    return t.Hour * 60 + t.Minute;
                }
            }
            return null;
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

        private static string Clean(string s)
        {
            return (s ?? "").Trim();
        }

        private static string DocId(BsonDocument d)
        {
            return d["_id"].ToString();
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

        private static string StatusOf(BsonDocument d)
        {
            return string.Equals(Str(d, "status"), "Unavailable", StringComparison.OrdinalIgnoreCase)
                ? "Unavailable" : "Active";
        }

        private static string NormalizeStatus(string s)
        {
            return string.Equals(Clean(s), "Unavailable", StringComparison.OrdinalIgnoreCase)
                ? "Unavailable" : "Active";
        }

        private static string PlainName(BsonDocument d)
        {
            return (Str(d, "firstName") + " " + Str(d, "lastName")).Trim();
        }

        // "506" -> "IK506";  "ik 506" -> "IK506";  "Auditorium" stays as typed.
        private static string RoomName(string typed, string code)
        {
            string t = Regex.Replace(Clean(typed), @"\s+", " ");
            if (t == "") return "";

            if (char.IsDigit(t[0])) return code + t.Replace(" ", "");
            if (t.StartsWith(code, StringComparison.OrdinalIgnoreCase))
            {
                return code + t.Substring(code.Length).Replace(" ", "");
            }
            return t;
        }

        private static string Plural(int n, string word)
        {
            return n + " " + word + (n == 1 ? "" : "s");
        }

        // ---------- schedule helpers (defense_schedules is owned by other modules) ----------

        // draft / pending / rejected schedules do not hold a room yet.
        private static bool HoldsRoom(BsonDocument d)
        {
            string s = Str(d, "status").Trim().ToLowerInvariant();
            return s != "draft" && s != "pending" && s != "rejected" && s != "declined";
        }

        private static bool IsCancelled(BsonDocument d)
        {
            string s = Str(d, "status").Trim().ToLowerInvariant();
            return s == "cancelled" || s == "canceled";
        }

        // How many live (not cancelled) defenses from today onwards use these rooms.
        private static int UpcomingDefenses(HashSet<string> roomIds, string today)
        {
            if (roomIds.Count == 0) return 0;

            return Schedules()
                .Find(Builders<BsonDocument>.Filter.Gte("date", today))
                .ToList()
                .Count(d => HoldsRoom(d) && !IsCancelled(d) && roomIds.Contains(AnyStr(d, "roomId")));
        }

        // ---------- responses ----------

        private JsonResult Ok(string message)
        {
            return Json(new { success = true, message = message });
        }

        private JsonResult Fail(string message)
        {
            return Json(new { success = false, message = message });
        }

        private JsonResult Expired()
        {
            return Json(new
            {
                success = false,
                unauthorized = true,
                message = "Your session has expired. Please log in again."
            }, JsonRequestBehavior.AllowGet);
        }

        private static string ErrorText(Exception ex)
        {
            bool debug = System.Web.HttpContext.Current != null
                && System.Web.HttpContext.Current.IsDebuggingEnabled;
            return debug ? "Server error: " + ex.Message : "Something went wrong. Please try again.";
        }

        private JsonResult Error(Exception ex, bool allowGet)
        {
            var data = new { success = false, message = ErrorText(ex) };
            return allowGet ? Json(data, JsonRequestBehavior.AllowGet) : Json(data);
        }

        // The signed-in campus director, re-checked against the database on every call
        // (so a deactivated or deleted account is kicked out immediately).
        private BsonDocument Director()
        {
            ObjectId oid;
            if (!ObjectId.TryParse(Convert.ToString(Session["CampusDirectorId"]), out oid))
            {
                return null;
            }

            BsonDocument doc = Users().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
            if (doc == null || !IsActive(doc) || AdminApi.RoleKey(Str(doc, "role")) != "campus-director")
            {
                Session.Remove("CampusDirectorId");
                return null;
            }
            return doc;
        }

        private static object MeRow(BsonDocument d)
        {
            string middle = Str(d, "middleName");
            if (middle == "") middle = Str(d, "middlename");

            return new
            {
                id = IdOf(d),
                firstName = Str(d, "firstName"),
                lastName = Str(d, "lastName"),
                middleName = middle,
                email = Str(d, "email"),
                role = "Campus Director"
            };
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
                    { "category", category }, // building | room
                    { "by", Convert.ToString(Session["CampusDirectorId"]) }
                });
            }
            catch
            {
                // Never fail the real action just because logging failed.
            }
        }

        // =========================== READ ===========================

        [HttpGet]
        public ActionResult me()
        {
            try
            {
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);

                BsonDocument user = Director();
                if (user == null) return Expired();

                return Json(new { success = true, me = MeRow(user) }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Error(ex, true);
            }
        }

        // Everything the dashboard, room schedule and room management pages need.
        // "available" on a room means the room AND its building are both Active.
        [HttpGet]
        public ActionResult state()
        {
            try
            {
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);

                BsonDocument user = Director();
                if (user == null) return Expired();

                var emptyFilter = new BsonDocument();
                List<BsonDocument> buildingDocs = Buildings()
    .Find(emptyFilter)
    .ToList()
    .Where(b =>
        !string.Equals(Str(b, "name").Trim(), "TBA",
            StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Str(b, "name").Trim(), "To Be Announced",
            StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Str(b, "code").Trim(), "TBA",
            StringComparison.OrdinalIgnoreCase)
    )
    .ToList();
                List<BsonDocument> roomDocs = Rooms().Find(emptyFilter).ToList();

                var buildingById = new Dictionary<string, BsonDocument>();
                foreach (BsonDocument b in buildingDocs) buildingById[DocId(b)] = b;

                var roomCounts = new Dictionary<string, int>();
                foreach (BsonDocument r in roomDocs)
                {
                    string bid = Str(r, "buildingId");
                    int n;
                    roomCounts.TryGetValue(bid, out n);
                    roomCounts[bid] = n + 1;
                }

                var buildings = buildingDocs
                    .OrderBy(b => Str(b, "name"), StringComparer.OrdinalIgnoreCase)
                    .Select(b =>
                    {
                        int n;
                        roomCounts.TryGetValue(DocId(b), out n);
                        return new
                        {
                            id = DocId(b),
                            name = Str(b, "name"),
                            code = Str(b, "code"),
                            status = StatusOf(b),
                            roomCount = n
                        };
                    })
                    .ToList();

                var rooms = roomDocs
                    .Select(r =>
                    {
                        string bid = Str(r, "buildingId");
                        BsonDocument b;
                        bool buildingOk = buildingById.TryGetValue(bid, out b) && StatusOf(b) == "Active";
                        string status = StatusOf(r);
                        return new
                        {
                            sortKey = (b != null ? Str(b, "name") : "~") + "|" + Str(r, "name"),
                            row = new
                            {
                                id = DocId(r),
                                buildingId = bid,
                                name = Str(r, "name"),
                                status = status,
                                available = status == "Active" && buildingOk
                            }
                        };
                    })
                    .OrderBy(x => x.sortKey, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.row)
                    .ToList();

                var activity = Activity()
                    .Find(new BsonDocument())
                    .Sort(Builders<BsonDocument>.Sort.Descending("ts"))
                    .Limit(50)
                    .ToList()
                    .Select(a =>
                    {
                        BsonValue ts;
                        DateTime when = DateTime.UtcNow;
                        if (a.TryGetValue("ts", out ts) && ts.IsBsonDateTime)
                        {
                            when = ts.ToUniversalTime();
                        }
                        return new
                        {
                            at = when.ToString("o"),
                            text = Str(a, "text"),
                            type = Str(a, "type"),
                            category = Str(a, "category")
                        };
                    })
                    .ToList();

                DateTime now = Manila();

                return Json(new
                {
                    success = true,
                    me = MeRow(user),
                    buildings = buildings,
                    rooms = rooms,
                    activity = activity,
                    today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    nowMinutes = now.Hour * 60 + now.Minute
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Error(ex, true);
            }
        }

        // Defense schedules that hold a room on one day (read-only).
        [HttpGet]
        public ActionResult schedule(string date)
        {
            try
            {
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);

                if (Director() == null) return Expired();

                DateTime now = Manila();
                DateTime day;
                if (!DateTime.TryParseExact(Clean(date), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out day))
                {
                    day = now.Date;
                }
                string dayText = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                var items = new List<object>();
                List<BsonDocument> docs = Schedules()
                    .Find(Builders<BsonDocument>.Filter.Eq("date", dayText))
                    .ToList();

                foreach (BsonDocument d in docs)
                {
                    if (!HoldsRoom(d)) continue;

                    int? start = Minutes(d, "start");
                    int? end = Minutes(d, "end");
                    string roomId = AnyStr(d, "roomId");
                    if (start == null || end == null || end.Value <= start.Value || roomId == "") continue;

                    var authors = new List<string>();
                    BsonValue av;
                    if (d.TryGetValue("authors", out av) && av != null && av.IsBsonArray)
                    {
                        foreach (BsonValue a in av.AsBsonArray)
                        {
                            if (a.IsString && a.AsString.Trim() != "") authors.Add(a.AsString.Trim());
                        }
                    }

                    items.Add(new
                    {
                        id = DocId(d),
                        roomId = roomId,
                        start = start.Value,
                        end = end.Value,
                        section = AnyStr(d, "section"),
                        group = AnyStr(d, "group"),
                        program = AnyStr(d, "program"),
                        title = AnyStr(d, "title"),
                        authors = authors,
                        cancelled = IsCancelled(d)
                    });
                }

                return Json(new
                {
                    success = true,
                    date = dayText,
                    items = items,
                    today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    nowMinutes = now.Hour * 60 + now.Minute
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Error(ex, true);
            }
        }

        // ========================= BUILDINGS =========================

        [HttpPost]
        public ActionResult saveBuilding(BuildingDto dto)
        {
            try
            {
                if (Director() == null) return Expired();
                if (dto == null) return Fail("No data received.");

                string name = Regex.Replace(Clean(dto.Name), @"\s+", " ");
                string code = Clean(dto.Code).ToUpperInvariant();
                
                if (string.Equals(name, "TBA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "To Be Announced", StringComparison.OrdinalIgnoreCase) ||
                code == "TBA")
                {
                    return Fail(
                        "TBA is not a physical building. Please enter a valid building."
                    );
                }

                if (name == "" || code == "") return Fail("Please enter the building name and code.");
                if (name.Length > 80) return Fail("Building name is too long (80 characters max).");
                if (!CodePattern.IsMatch(code))
                {
                    return Fail("Building code must be 1 to 10 letters or numbers, with no spaces.");
                }

                bool isEdit = Clean(dto.Id) != "";
                ObjectId oid = ObjectId.Empty;
                BsonDocument existing = null;

                if (isEdit)
                {
                    if (!ObjectId.TryParse(Clean(dto.Id), out oid)) return Fail("Building not found.");
                    existing = Buildings().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                    if (existing == null) return Fail("Building not found.");
                }
                string selfId = isEdit ? oid.ToString() : "";

                foreach (BsonDocument b in Buildings().Find(new BsonDocument()).ToList())
                {
                    if (DocId(b) == selfId) continue;

                    if (string.Equals(Str(b, "name").Trim(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail("A building named \"" + Str(b, "name") + "\" already exists.");
                    }
                    if (string.Equals(Str(b, "code").Trim(), code, StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail("The code " + Str(b, "code") + " is already used by " + Str(b, "name") + ".");
                    }
                }

                DateTime now = DateTime.UtcNow;

                if (!isEdit)
                {
                    Buildings().InsertOne(new BsonDocument
                    {
                        { "name", name },
                        { "code", code },
                        { "status", "Active" },
                        { "createdAt", now },
                        { "updatedAt", now }
                    });
                    Log("Building " + name + " added", "create", "building");
                    return Ok("Building added.");
                }

                string status = NormalizeStatus(dto.Status);
                string oldStatus = StatusOf(existing);

                var u = Builders<BsonDocument>.Update;
                Buildings().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    u.Combine(
                        u.Set("name", name),
                        u.Set("code", code),
                        u.Set("status", status),
                        u.Set("updatedAt", now)));

                Log(status != oldStatus
                        ? "Building " + name + " status changed to " + status
                        : "Building " + name + " details updated",
                    "edit", "building");
                return Ok("Building updated.");
            }
            catch (Exception ex)
            {
                return Error(ex, false);
            }
        }

        [HttpPost]
        public ActionResult deleteBuilding(CdIdDto dto)
        {
            try
            {
                if (Director() == null) return Expired();

                ObjectId oid;
                if (dto == null || !ObjectId.TryParse(Clean(dto.Id), out oid))
                {
                    return Fail("Building not found.");
                }

                BsonDocument doc = Buildings().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                if (doc == null) return Fail("Building not found.");

                string buildingId = oid.ToString();
                List<BsonDocument> roomDocs = Rooms()
                    .Find(Builders<BsonDocument>.Filter.Eq("buildingId", buildingId))
                    .ToList();

                // Defense schedules belong to the Coordinator / Dean modules, so they are
                // never deleted from here. Deleting is blocked while any still need these rooms.
                var roomIds = new HashSet<string>(roomDocs.Select(r => DocId(r)));
                string today = Manila().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                int upcoming = UpcomingDefenses(roomIds, today);
                if (upcoming > 0)
                {
                    return Fail("This building's rooms still have " + Plural(upcoming, "upcoming defense schedule")
                        + ". Cancel or reschedule them first.");
                }

                Rooms().DeleteMany(Builders<BsonDocument>.Filter.Eq("buildingId", buildingId));
                Buildings().DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", oid));

                Log("Building " + Str(doc, "name") + " deleted", "delete", "building");
                return Ok("Building deleted.");
            }
            catch (Exception ex)
            {
                return Error(ex, false);
            }
        }

        // ============================ ROOMS ==========================

        [HttpPost]
        public ActionResult saveRoom(RoomDto dto)
        {
            try
            {
                if (Director() == null) return Expired();
                if (dto == null) return Fail("No data received.");

                ObjectId buildingOid;
                if (!ObjectId.TryParse(Clean(dto.BuildingId), out buildingOid))
                {
                    return Fail("Please choose a building.");
                }
                BsonDocument building = Buildings()
                    .Find(Builders<BsonDocument>.Filter.Eq("_id", buildingOid)).FirstOrDefault();
                if (building == null) return Fail("Please choose a building.");
                 
                if (string.Equals(Str(building, "name").Trim(), "TBA",
        StringComparison.OrdinalIgnoreCase) ||
    string.Equals(Str(building, "code").Trim(), "TBA",
        StringComparison.OrdinalIgnoreCase) ||
    string.Equals(Str(building, "name").Trim(), "To Be Announced",
        StringComparison.OrdinalIgnoreCase))
                {
                    return Fail("Cannot add a room under TBA.");
                }

                string buildingId = buildingOid.ToString();
                string name = RoomName(dto.Name, Str(building, "code"));
                if (name == "") return Fail("Please enter a room number.");
                if (name.Length > 30) return Fail("Room name is too long (30 characters max).");

                bool isEdit = Clean(dto.Id) != "";
                ObjectId oid = ObjectId.Empty;
                BsonDocument existing = null;

                if (isEdit)
                {
                    if (!ObjectId.TryParse(Clean(dto.Id), out oid)) return Fail("Room not found.");
                    existing = Rooms().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                    if (existing == null) return Fail("Room not found.");
                }
                string selfId = isEdit ? oid.ToString() : "";

                foreach (BsonDocument r in Rooms().Find(Builders<BsonDocument>.Filter.Eq("buildingId", buildingId)).ToList())
                {
                    if (DocId(r) != selfId
                        && string.Equals(Str(r, "name").Trim(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail(name + " already exists in " + (isEdit ? "that" : "this") + " building.");
                    }
                }

                DateTime now = DateTime.UtcNow;

                if (!isEdit)
                {
                    Rooms().InsertOne(new BsonDocument
                    {
                        { "buildingId", buildingId },
                        { "name", name },
                        { "status", "Active" },
                        { "createdAt", now },
                        { "updatedAt", now }
                    });
                    Log("Room " + name + " added to " + Str(building, "name"), "create", "room");
                    return Ok("Room added.");
                }

                string status = NormalizeStatus(dto.Status);
                string oldStatus = StatusOf(existing);
                string oldName = Str(existing, "name");

                // Making a room unavailable, or moving it to another building, must not
                // strand a defense that is still coming up.
                bool movedOut = Str(existing, "buildingId") != buildingId;
                bool goingUnavailable = status == "Unavailable" && oldStatus != "Unavailable";
                if (movedOut || goingUnavailable)
                {
                    string today = Manila().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    int upcoming = UpcomingDefenses(new HashSet<string> { selfId }, today);
                    if (upcoming > 0)
                    {
                        return Fail(oldName + " still has " + Plural(upcoming, "upcoming defense schedule")
                            + ". Cancel or reschedule them before "
                            + (goingUnavailable ? "making the room unavailable." : "moving the room to another building."));
                    }
                }

                var u = Builders<BsonDocument>.Update;
                Rooms().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    u.Combine(
                        u.Set("buildingId", buildingId),
                        u.Set("name", name),
                        u.Set("status", status),
                        u.Set("updatedAt", now)));

                Log(status != oldStatus
                        ? "Room " + name + " status changed to " + status
                        : "Room " + name + " details updated",
                    "edit", "room");
                return Ok("Room updated.");
            }
            catch (Exception ex)
            {
                return Error(ex, false);
            }
        }

        [HttpPost]
        public ActionResult deleteRoom(CdIdDto dto)
        {
            try
            {
                if (Director() == null) return Expired();

                ObjectId oid;
                if (dto == null || !ObjectId.TryParse(Clean(dto.Id), out oid))
                {
                    return Fail("Room not found.");
                }

                BsonDocument doc = Rooms().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
                if (doc == null) return Fail("Room not found.");

                string today = Manila().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                int upcoming = UpcomingDefenses(new HashSet<string> { oid.ToString() }, today);
                if (upcoming > 0)
                {
                    return Fail(Str(doc, "name") + " still has " + Plural(upcoming, "upcoming defense schedule")
                        + ". Cancel or reschedule them first.");
                }

                Rooms().DeleteOne(Builders<BsonDocument>.Filter.Eq("_id", oid));

                Log("Room " + Str(doc, "name") + " deleted", "delete", "room");
                return Ok("Room deleted.");
            }
            catch (Exception ex)
            {
                return Error(ex, false);
            }
        }

        // =========================== PROFILE ==========================

        // Edit Profile: name and e-mail (the picture is replaced by the user's initials).
        [HttpPost]
        public ActionResult updateProfile(Subsystem1.Data.ProfileDto dto)
        {
            try
            {
                BsonDocument user = Director();
                if (user == null) return Expired();

                string first, middle, last, email;
                string error = Subsystem1.Data.ProfileUpdater.Update(user, dto, out first, out middle, out last, out email);
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
                return Error(ex, false);
            }
        }

        [HttpPost]
        public ActionResult changePassword(PasswordDto dto)
        {
            try
            {
                BsonDocument user = Director();
                if (user == null) return Expired();
                if (dto == null) return Fail("No data received.");

                string current = dto.CurrentPassword ?? "";
                string next = dto.NewPassword ?? "";

                if (current == "" || next == "") return Fail("Please fill in all password fields.");
                if (next.Length < 8) return Fail("New password must be at least 8 characters long.");
                if (next.Length > 128) return Fail("New password is too long (128 characters max).");

                bool currentOk;
                try
                {
                    currentOk = BCrypt.Net.BCrypt.Verify(current, Str(user, "password"));
                }
                catch
                {
                    currentOk = false;
                }
                if (!currentOk) return Fail("Incorrect current password.");

                if (current == next) return Fail("New password must be different from the current password.");

                var u = Builders<BsonDocument>.Update;
                Users().UpdateOne(
                    Builders<BsonDocument>.Filter.Eq("_id", user["_id"]),
                    u.Combine(
                        u.Set("password", BCrypt.Net.BCrypt.HashPassword(next)),
                        u.Set("updatedAt", DateTime.UtcNow)));

                return Ok("Password successfully updated!");
            }
            catch (Exception ex)
            {
                return Error(ex, false);
            }
        }
    }
}
