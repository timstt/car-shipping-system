using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // Таблиця Orders: перегляд, додавання, зміна, видалення
    public partial class AnalyticsController
    {
        // GET: Analytics/OrdersTable
        [HttpGet]
        public ActionResult OrdersTable()
        {
            var rows = (from o in AnalyticsDb.Orders
                        join cl in AnalyticsDb.Users on o.ClientId equals cl.Id
                        join m in AnalyticsDb.Users on o.ManagerId equals (int?)m.Id into managers
                        from m in managers.DefaultIfEmpty()
                        join c in AnalyticsDb.Cars on o.CarId equals (int?)c.CarId into cars
                        from c in cars.DefaultIfEmpty()
                        orderby o.OrderId
                        select new OrderTableRow
                        {
                            OrderId = o.OrderId,
                            ClientName = cl.Name,
                            ManagerName = m.Name,
                            Make = c.Make,
                            Model = c.Model,
                            MaxBudget = o.MaxBudget,
                            FinalPrice = o.FinalPrice,
                            Status = o.Status,
                            CreatedAt = o.CreatedAt
                        }).ToList();
            return View(rows);
        }

        // GET: Analytics/CreateOrder
        [HttpGet]
        public ActionResult CreateOrder()
        {
            return OrderForm(new OrderInput { Status = OrderStatus.New.ToString(), CreatedAt = DateTime.Today });
        }

        // POST: Analytics/CreateOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateOrder([Bind(Include = "ClientId,ManagerId,CarId,MaxBudget,FinalPrice,Status,CreatedAt")] OrderInput input)
        {
            ValidateOrderInput(input, null);
            if (!ModelState.IsValid) return OrderForm(input);

            var order = new AnalyticsOrder();
            CopyOrder(input, order);
            AnalyticsDb.Orders.Add(order);
            if (!TrySave("Не вдалося додати заявку. Перевірте введені дані.")) return OrderForm(input);

            TempData["Message"] = "Заявку №" + order.OrderId + " додано.";
            return RedirectToAction("OrdersTable");
        }

        // GET: Analytics/EditOrder/5
        [HttpGet]
        public ActionResult EditOrder(int? id)
        {
            var order = id.HasValue ? AnalyticsDb.Orders.Find(id.Value) : null;
            if (order == null) return NotFoundRedirect("Заявку не знайдено.", "OrdersTable");

            return OrderForm(new OrderInput
            {
                OrderId = order.OrderId,
                ClientId = order.ClientId,
                ManagerId = order.ManagerId,
                CarId = order.CarId,
                MaxBudget = order.MaxBudget,
                FinalPrice = order.FinalPrice,
                Status = order.Status,
                CreatedAt = order.CreatedAt
            });
        }

        // POST: Analytics/EditOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditOrder([Bind(Include = "OrderId,ClientId,ManagerId,CarId,MaxBudget,FinalPrice,Status,CreatedAt")] OrderInput input)
        {
            var order = input.OrderId.HasValue ? AnalyticsDb.Orders.Find(input.OrderId.Value) : null;
            if (order == null) return NotFoundRedirect("Заявку не знайдено.", "OrdersTable");

            // Завершена заявка — закрита історія: змінити в ній нічого не можна
            if (order.Status == OrderStatus.Completed.ToString())
            {
                ModelState.AddModelError("", "Завершену заявку змінити не можна");
                return OrderForm(input);
            }

            ValidateOrderInput(input, order);
            if (!ModelState.IsValid) return OrderForm(input);

            CopyOrder(input, order);
            if (!TrySave("Не вдалося змінити заявку. Перевірте введені дані.")) return OrderForm(input);

            TempData["Message"] = "Заявку №" + order.OrderId + " змінено.";
            return RedirectToAction("OrdersTable");
        }

        // POST: Analytics/DeleteOrder/5 — заявку зі страховками, інвойсом чи доставкою видалити не можна
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteOrder(int? id)
        {
            var order = id.HasValue ? AnalyticsDb.Orders.Find(id.Value) : null;
            if (order == null) return NotFoundRedirect("Заявку не знайдено.", "OrdersTable");

            if (order.Status == OrderStatus.Completed.ToString())
            {
                TempData["Message"] = "Заявку №" + order.OrderId + " не можна видалити: її вже завершено.";
                return RedirectToAction("OrdersTable");
            }

            var blockers = new List<string>();
            int insurances = AnalyticsDb.Insurances.Count(i => i.OrderId == order.OrderId);
            if (insurances > 0) blockers.Add("страховки (" + insurances + ")");
            if (AnalyticsDb.Invoices.Any(i => i.OrderId == order.OrderId)) blockers.Add("інвойс");
            if (AnalyticsDb.Deliveries.Any(d => d.OrderId == order.OrderId)) blockers.Add("доставка");
            if (blockers.Any())
            {
                TempData["Message"] = string.Format("Заявку №{0} не можна видалити: спершу видаліть пов'язані записи — {1}.",
                    order.OrderId, string.Join(", ", blockers));
                return RedirectToAction("OrdersTable");
            }

            AnalyticsDb.Orders.Remove(order);
            TempData["Message"] = TrySave("")
                ? "Заявку №" + id.Value + " видалено."
                : "Заявку №" + id.Value + " не вдалося видалити.";
            return RedirectToAction("OrdersTable");
        }

        private ActionResult OrderForm(OrderInput input)
        {
            ViewBag.Clients = AnalyticsChoices.Pairs(UsersByRole(UserRole.Client), input.ClientId);
            ViewBag.Managers = AnalyticsChoices.Pairs(UsersByRole(UserRole.Manager), input.ManagerId);
            var cars = AnalyticsDb.Cars.OrderBy(c => c.Make).ThenBy(c => c.Model).ToList()
                .Select(c => new KeyValuePair<int, string>(c.CarId,
                    string.Format("{0} {1} {2} — {3}", c.Make, c.Model, c.Year, c.VinCode)));
            ViewBag.Cars = AnalyticsChoices.Pairs(cars, input.CarId);
            ViewBag.Statuses = AnalyticsChoices.Enum<OrderStatus>(input.Status);
            return View("OrderForm", input);
        }

        private void ValidateOrderInput(OrderInput input, AnalyticsOrder existing)
        {
            // Оплачену заявку (є оплачений інвойс чи доставка) не можна повернути до неоплаченого статусу
            if (existing != null && input.Status != null && !PaidOrderStatuses.Contains(input.Status)
                && (AnalyticsDb.Deliveries.Any(d => d.OrderId == existing.OrderId)
                    || AnalyticsDb.Invoices.Any(i => i.OrderId == existing.OrderId && i.IsPaid)))
                ModelState.AddModelError("Status", "Заявку вже оплачено: її статус може бути лише " + PaidOrderStatusNames());

            if (!string.IsNullOrEmpty(input.Status) && !AnalyticsChoices.IsEnumName<OrderStatus>(input.Status))
                ModelState.AddModelError("Status", "Невідомий статус заявки");
            if (input.ClientId.HasValue && !UserHasRole(input.ClientId, UserRole.Client))
                ModelState.AddModelError("ClientId", "Оберіть користувача з роллю «Клієнт»");
            if (input.ManagerId.HasValue && !UserHasRole(input.ManagerId, UserRole.Manager))
                ModelState.AddModelError("ManagerId", "Оберіть користувача з роллю «Менеджер»");
            if (input.CarId.HasValue && !AnalyticsDb.Cars.Any(c => c.CarId == input.CarId.Value))
                ModelState.AddModelError("CarId", "Авто не знайдено");
            if (input.CreatedAt.HasValue && (input.CreatedAt.Value.Year < 2000 || input.CreatedAt.Value.Year > 2100))
                ModelState.AddModelError("CreatedAt", "Дата має бути в межах 2000–2100 років");
        }

        private static void CopyOrder(OrderInput input, AnalyticsOrder order)
        {
            order.ClientId = input.ClientId.Value;
            order.ManagerId = input.ManagerId;
            order.CarId = input.CarId;
            order.MaxBudget = input.MaxBudget.Value;
            order.FinalPrice = input.FinalPrice;
            order.Status = input.Status;
            // Час створення зберігаємо, якщо дату не змінили; нова дата — північ цього дня
            if (order.CreatedAt.Date != input.CreatedAt.Value.Date)
                order.CreatedAt = input.CreatedAt.Value.Date;
        }
    }
}
