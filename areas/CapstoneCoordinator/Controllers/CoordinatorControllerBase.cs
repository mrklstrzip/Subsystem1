using System;
using System.Web;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.Student.Models;
using AdminApi = Subsystem1.Areas.Admin.Controllers.AdminApiController;

namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class CoordinatorAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            string area = filterContext.RouteData.DataTokens["area"] as string;
            if (!string.Equals(area, "CapstoneCoordinator", StringComparison.OrdinalIgnoreCase))
            {
                return; // not a Coordinator page
            }

            string controller = Convert.ToString(filterContext.RouteData.Values["controller"]);

            var http = filterContext.HttpContext;
            bool loggedIn = http.Session != null && http.Session["CoordinatorId"] != null;
            bool isApi = string.Equals(controller, "CoordinatorApi", StringComparison.OrdinalIgnoreCase);

            if (loggedIn)
            {
                // Block cross-site posts to the JSON API (same check as the other areas).
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
                // Status 200 on purpose: a 401 would be turned into a login redirect
                // by Forms Authentication and break fetch().
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

    // Base class for every Coordinator controller: loads the signed-in coordinator from the
    // database on each request (so a deactivated or deleted account is kicked out at once).
    public abstract class CoordinatorControllerBase : Controller
    {
        protected BsonDocument Me { get; private set; }

        protected virtual bool IsApi { get { return false; } }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Me = LoadCoordinator(Session);

            if (Me == null)
            {
                filterContext.Result = IsApi
                    ? (ActionResult)Json(new
                    {
                        success = false,
                        unauthorized = true,
                        message = "Your session has expired. Please log in again."
                    }, JsonRequestBehavior.AllowGet)
                    : new RedirectResult("~/Auth/Login/login");
                return;
            }

            // So the Back button cannot show these pages after logging out.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            base.OnActionExecuting(filterContext);
        }

        public static BsonDocument LoadCoordinator(HttpSessionStateBase session)
        {
            ObjectId oid;
            if (session == null || !ObjectId.TryParse(Convert.ToString(session["CoordinatorId"]), out oid))
            {
                return null;
            }

            BsonDocument doc = StudentDb.Users().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
            if (doc == null
                || !StudentDb.IsActive(doc)
                || AdminApi.RoleKey(StudentDb.Str(doc, "role")) != "capstone-coordinator")
            {
                session.Remove("CoordinatorId");
                return null;
            }
            return doc;
        }
    }
}
