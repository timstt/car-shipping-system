using System.Data.Entity;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using ASTCUSA.Models;

namespace ASTCUSA
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Code First: створює БД за моделлю і заповнює її тестовими даними (Seed)
            Database.SetInitializer(new ImportInitializer());

            // Українські тексти стандартних повідомлень MVC з App_GlobalResources/ValidationMessages.resx:
            // «некоректне значення» (сервер) і «має бути числом / датою» (клієнт)
            DefaultModelBinder.ResourceClassKey = "ValidationMessages";
            ClientDataTypeModelValidatorProvider.ResourceClassKey = "ValidationMessages";

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
