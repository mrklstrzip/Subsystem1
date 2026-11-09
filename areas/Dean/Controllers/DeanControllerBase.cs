using System;
using System.Web;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using AdminApi = Subsystem1.Areas.Admin.Controllers.AdminApiController;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DeanAuthorizeAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            string area = filterContext.RouteData.DataTokens["area"] as string;
            if (!string.Equals(area, "Dean", StringComparison.OrdinalIgnoreCase))
            {
                return; // not a Dean page
            }

            string controller = Convert.ToString(filterContext.RouteData.Values["controller"]);
            var http = filterContext.HttpContext;
            bool loggedIn = http.Session != null && http.Session["DeanId"] != null;
            bool isApi = string.Equals(controller, "DeanApi", StringComparison.OrdinalIgnoreCase);
            bool ajax = string.Equals(http.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal);

            if (loggedIn)
            {
                // Block cross-site posts to the JSON API.
                if (isApi
                    && string.Equals(http.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                    && !ajax)
                {
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, message = "Invalid request." },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                return;
            }

            if (isApi || ajax)
            {
                // Status 200 on purpose: a 401 would be turned into a login redirect by Forms Authentication.
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, unauthorized = true, message = "Your session has expired. Please log in again." },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                filterContext.Result = new RedirectResult("~/Auth/Login/login");
            }
        }
    }

    // Base class for every Dean controller: loads the signed-in dean from the database on each
    // request (so a deactivated or deleted account is kicked out at once).
    public abstract class DeanControllerBase : Controller
    {
        protected BsonDocument Me { get; private set; }

        protected virtual bool IsApi
        {
            get { return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal); }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Me = LoadDean(Session);

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

        public static BsonDocument LoadDean(HttpSessionStateBase session)
        {
            ObjectId oid;
            if (session == null || !ObjectId.TryParse(Convert.ToString(session["DeanId"]), out oid))
            {
                return null;
            }

            BsonDocument doc = SharedDb.Users().Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefault();
            if (doc == null
                || !SharedDb.IsActive(doc)
                || AdminApi.RoleKey(SharedDb.Str(doc, "role")) != "dean")
            {
                session.Remove("DeanId");
                return null;
            }
            return doc;
        }
    }
}
