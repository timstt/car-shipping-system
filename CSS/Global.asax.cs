using System.Data.Entity;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using ASTCUSA.Models;
using ASTCUSA.Models.Lab3;

namespace ASTCUSA
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Code First: створює БД за моделлю і заповнює її тестовими даними (Seed)
            Database.SetInitializer(new ImportInitializer());

            // Базу astcusa_lab3 створює SQL-скрипт (Lab3Database), а не EF,
            // тому ініціалізатор для Lab3Context вимкнено — EF не створює й не змінює цю базу
            Database.SetInitializer<Lab3Context>(null);

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
