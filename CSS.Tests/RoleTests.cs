using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Web.Mvc;
using ASTCUSA.Controllers;
using ASTCUSA.Models;
using ASTCUSA.Tests.Infrastructure;
using Xunit;

namespace ASTCUSA.Tests
{
    // ЛР5: перевірка ролей — хто яку дію може виконати і які дані бачить
    public class RoleTests
    {
        // ---------- Матриця ролей (статично, за атрибутами) ----------

        private static UserRole[] RolesOf(Type controller, string action)
        {
            var method = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => typeof(ActionResult).IsAssignableFrom(m.ReturnType) &&
                            (m.Name == action || (m.GetCustomAttribute<ActionNameAttribute>() != null &&
                                                  m.GetCustomAttribute<ActionNameAttribute>().Name == action)))
                .OrderByDescending(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
                .First();
            var attr = method.GetCustomAttribute<AllowRolesAttribute>() ?? controller.GetCustomAttribute<AllowRolesAttribute>();
            if (attr == null) return new UserRole[0];
            var field = typeof(AllowRolesAttribute).GetField("roles", BindingFlags.NonPublic | BindingFlags.Instance);
            return (UserRole[])field.GetValue(attr);
        }

        // Очікувані ролі для дій, що змінюють дані (за діаграмою прецедентів і README)
        public static IEnumerable<object[]> ExpectedRoles()
        {
            var client = UserRole.Client;
            var manager = UserRole.Manager;
            var logist = UserRole.Logistician;
            var c = typeof(CarsController);
            var o = typeof(OrdersController);
            return new[]
            {
                new object[] { c, "Create", manager }, new object[] { c, "Edit", manager }, new object[] { c, "Delete", manager },
                new object[] { c, "AddToFavorites", client }, new object[] { c, "Favorites", client },
                new object[] { o, "Create", client }, new object[] { o, "Edit", client }, new object[] { o, "Cancel", client },
                new object[] { o, "ProvideDocuments", client }, new object[] { o, "AddInsurance", client },
                new object[] { o, "EditInsurance", client }, new object[] { o, "DeleteInsurance", client },
                new object[] { o, "PayInvoice", client }, new object[] { o, "ConfirmReceipt", client }, new object[] { o, "LeaveReview", client },
                new object[] { o, "ProcessOrder", manager }, new object[] { o, "RequestDocuments", manager }, new object[] { o, "PlaceBid", manager },
                new object[] { o, "CreateInvoice", manager }, new object[] { o, "ConfirmPayment", manager }, new object[] { o, "RejectPayment", manager },
                new object[] { o, "Delete", manager },
                new object[] { o, "CreateDelivery", logist }, new object[] { o, "GenerateMandate", logist }, new object[] { o, "AddCargoInsurance", logist },
                new object[] { o, "AddPhotoReport", logist }, new object[] { o, "ClearCustoms", logist }, new object[] { o, "UpdateDeliveryStatus", logist },
                new object[] { typeof(ReportsController), "Export", manager },
                new object[] { typeof(AnalyticsController), "DeleteOrder", manager },
                new object[] { typeof(ProfileController), "Edit", client }, new object[] { typeof(ProfileController), "Delete", client },
            };
        }

        [Theory]
        [MemberData(nameof(ExpectedRoles))]
        public void Action_is_allowed_only_for_its_role(Type controller, string action, UserRole role)
        {
            Assert.Equal(new[] { role }, RolesOf(controller, action));
        }

        // Жодна дія, що змінює дані (POST), не лишилася без обмеження ролі (крім входу/реєстрації/виходу)
        [Fact]
        public void Every_post_action_has_role_restriction()
        {
            var controllers = typeof(BaseController).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(BaseController)) && !t.IsAbstract && t != typeof(AccountController));
            var unprotected = new List<string>();
            foreach (var type in controllers)
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (m.GetCustomAttribute<HttpPostAttribute>() == null) continue;
                    if (m.GetCustomAttribute<AllowRolesAttribute>() == null && type.GetCustomAttribute<AllowRolesAttribute>() == null)
                        unprotected.Add(type.Name + "." + m.Name);
                }
            }
            Assert.Empty(unprotected);
        }

        // Глобальний [Authorize]: гість не відкриє жодної сторінки, крім позначених [AllowAnonymous]
        [Fact]
        public void Global_authorize_filter_is_registered()
        {
            var filters = new GlobalFilterCollection();
            FilterConfig.RegisterGlobalFilters(filters);
            Assert.Contains(filters, f => f.Instance is AuthorizeAttribute);
        }

        // ---------- Поведінка фільтра AllowRoles ----------

        private class DummyController : Controller { }

        private static ActionExecutingContext Execute(IPrincipal user, params UserRole[] allowed)
        {
            var http = Web.Http(user);
            var controller = new DummyController();
            var context = new ActionExecutingContext
            {
                HttpContext = http,
                Controller = controller,
                RouteData = new System.Web.Routing.RouteData()
            };
            controller.ControllerContext = new ControllerContext(http, context.RouteData, controller);
            new AllowRolesAttribute(allowed).OnActionExecuting(context);
            return context;
        }

        [Fact]
        public void Filter_lets_allowed_role_through()
        {
            var context = Execute(Web.Principal(1, UserRole.Manager), UserRole.Manager);
            Assert.Null(context.Result);
        }

        [Fact]
        public void Filter_blocks_other_role_with_message()
        {
            var context = Execute(Web.Principal(1, UserRole.Client), UserRole.Manager);
            Assert.IsType<RedirectResult>(context.Result);
            Assert.Contains("Менеджер", (string)context.Controller.TempData["Message"]);
        }

        [Fact]
        public void Filter_requires_login_for_guest()
        {
            var context = Execute(Web.Anonymous(), UserRole.Client);
            Assert.IsType<HttpUnauthorizedResult>(context.Result);
        }

        // ---------- Доступ до даних (рівень записів) ----------

        // Клієнт бачить лише свої заявки; чужу не відкриє й не змінить
        [Fact]
        public void Client_sees_and_edits_only_own_orders()
        {
            var seed = TestDb.Reset();
            int own = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.New);
            int foreign = TestDb.AddOrder(seed.Client2Id, seed.CivicId, OrderStatus.New, budget: 20000);

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
            {
                var list = (IEnumerable<Order>)Assert.IsType<ViewResult>(c.Index(null)).Model;
                Assert.Equal(new[] { own }, list.Select(o => o.OrderId).ToArray());

                Assert.IsType<RedirectToRouteResult>(c.Details(foreign)); // «Заявку не знайдено»
                Assert.IsType<ViewResult>(c.Details(own));
            }

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                c.Edit(new Order { OrderId = foreign, MaxBudget = 1, CarId = seed.CivicId });
            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                c.Cancel(foreign);
            using (var db = new ImportContext())
            {
                var order = db.Orders.Find(foreign);
                Assert.Equal(20000, order.MaxBudget);
                Assert.Equal(OrderStatus.New, order.Status);
            }
        }

        // Логіст бачить лише оплачені заявки; менеджер — усі
        [Fact]
        public void Logistician_sees_only_paid_orders_manager_sees_all()
        {
            var seed = TestDb.Reset();
            int fresh = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.New);
            int paid = TestDb.AddOrder(seed.Client2Id, seed.CivicId, OrderStatus.Paid);

            using (var c = Web.Controller<OrdersController>(seed.LogisticianId, UserRole.Logistician))
            {
                var list = (IEnumerable<Order>)Assert.IsType<ViewResult>(c.Index(null)).Model;
                Assert.Equal(new[] { paid }, list.Select(o => o.OrderId).ToArray());
                Assert.IsType<RedirectToRouteResult>(c.Details(fresh));
            }
            using (var c = Web.Controller<OrdersController>(seed.ManagerId, UserRole.Manager))
            {
                var list = (IEnumerable<Order>)Assert.IsType<ViewResult>(c.Index(null)).Model;
                Assert.Equal(2, list.Count());
            }
        }

        // Клієнт бачить у каталозі лише доступні авто, менеджер — усі
        [Fact]
        public void Client_catalog_shows_only_available_cars()
        {
            var seed = TestDb.Reset();
            using (var db = new ImportContext())
            {
                db.Cars.Find(seed.CivicId).Status = CarStatus.Reserved;
                db.SaveChanges();
            }

            using (var c = Web.Controller<CarsController>(seed.Client1Id, UserRole.Client))
            {
                var model = (CarCatalogViewModel)Assert.IsType<ViewResult>(c.Index(new CarSearchFilter())).Model;
                Assert.Equal(new[] { seed.CamryId }, model.Cars.Select(x => x.CarId).ToArray());
            }
            using (var c = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
            {
                var model = (CarCatalogViewModel)Assert.IsType<ViewResult>(c.Index(new CarSearchFilter())).Model;
                Assert.Equal(2, model.Cars.Count());
            }
        }

        // Доменні правила ролей: менеджер не візьме в роботу заявку на іншому етапі
        [Fact]
        public void Manager_cannot_take_order_in_wrong_status()
        {
            var manager = new Manager { Id = 1 };
            var order = new Order { Status = OrderStatus.Paid };
            Assert.Throws<InvalidOperationException>(() => manager.TakeOrderInWork(order));
        }
    }
}
