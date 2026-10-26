using System.Web;
using System.Web.Mvc;

namespace ASTCUSA
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Усі дії вимагають входу; гостям відкриті лише дії з [AllowAnonymous].
            // Неавторизованого FormsAuthentication перенаправляє на ~/Account/Login.
            filters.Add(new AuthorizeAttribute());
        }
    }
}
