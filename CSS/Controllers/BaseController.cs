using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // Спільна логіка: поточний користувач і роль з автентифікованого користувача (User.Identity,
    // тікет FormsAuthentication), передача їх у подання через ViewBag.
    // Вхід вимагає глобальний фільтр [Authorize] (FilterConfig); відкриті дії позначено [AllowAnonymous].
    public abstract class BaseController : Controller
    {
        protected readonly ImportContext db = new ImportContext();

        protected UserRole CurrentRole
        {
            get { return User.Identity.GetUserRole(); }
        }

        protected int? CurrentUserId
        {
            get { return User.Identity.GetUserId(); }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Тікет дійсний, але користувача вже немає в БД (наприклад, базу перестворено) — вийти
            if (Request.IsAuthenticated)
            {
                int? id = CurrentUserId;
                var user = id.HasValue ? db.Users.Find(id.Value) : null;
                if (user == null || user.Role != CurrentRole)
                {
                    Models.User.SignOut(HttpContext);
                    filterContext.Result = new RedirectResult(FormsAuthentication.LoginUrl);
                    return;
                }
            }

            ViewBag.CurrentRole = CurrentRole;
            ViewBag.CurrentUserName = User.Identity.GetDisplayName();
            base.OnActionExecuting(filterContext);
        }

        // Показати повідомлення і повернутися на сторінку заявки
        protected ActionResult BackToOrder(int id, string message)
        {
            if (!string.IsNullOrEmpty(message)) TempData["Message"] = message;
            return RedirectToAction("Details", "Orders", new { id = id });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // Обмеження доступу до дії за ролями з діаграми прецедентів
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AllowRolesAttribute : ActionFilterAttribute
    {
        private readonly UserRole[] roles;

        public AllowRolesAttribute(params UserRole[] roles)
        {
            this.roles = roles;
            Order = 1; // виконується після перевірки користувача в BaseController
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            if (!http.Request.IsAuthenticated)
            {
                // FormsAuthentication перетворює 401 на редирект на Account/Login?ReturnUrl=...
                filterContext.Result = new HttpUnauthorizedResult();
                return;
            }

            var role = http.User.Identity.GetUserRole();
            if (roles.Contains(role)) return;

            var allowed = string.Join(", ", roles.Select(r => r.GetDisplayName()));
            filterContext.Controller.TempData["Message"] = "Ця дія доступна лише для ролі: " + allowed + ".";

            // Повертаємося назад лише в межах сайту
            var referrer = http.Request.UrlReferrer;
            bool localReferrer = referrer != null && referrer.IsAbsoluteUri &&
                string.Equals(referrer.Host, http.Request.Url.Host, StringComparison.OrdinalIgnoreCase) &&
                referrer.Port == http.Request.Url.Port;
            filterContext.Result = localReferrer
                ? new RedirectResult(referrer.PathAndQuery)
                : new RedirectResult("~/");
        }
    }
}
