using System;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // Таблиця Deliveries: перегляд, додавання, зміна, видалення.
    // Ключ доставки — номер заявки (Order 1 — 0..1 Delivery), тому при зміні його не можна змінити.
    // (вкладка «Доставки» з ЛР3 — це окремий пошук, вона не змінюється)
    public partial class AnalyticsController
    {
        // GET: Analytics/DeliveriesTable
        [HttpGet]
        public ActionResult DeliveriesTable()
        {
            var rows = (from d in AnalyticsDb.Deliveries
                        join o in AnalyticsDb.Orders on d.OrderId equals o.OrderId
                        join u in AnalyticsDb.Users on o.ClientId equals u.Id
                        join l in AnalyticsDb.Users on d.LogisticianId equals (int?)l.Id into logisticians
                        from l in logisticians.DefaultIfEmpty()
                        orderby d.OrderId
                        select new DeliveryTableRow
                        {
                            OrderId = d.OrderId,
                            ClientName = u.Name,
                            LogisticianName = l.Name,
                            TrackingNumber = d.TrackingNumber,
                            CurrentLocation = d.CurrentLocation,
                            IsCustomsCleared = d.IsCustomsCleared,
                            Status = d.Status
                        }).ToList();
            return View(rows);
        }

        // GET: Analytics/CreateDelivery
        [HttpGet]
        public ActionResult CreateDelivery()
        {
            return DeliveryForm(new DeliveryInput { Status = DeliveryStages[0] }, true);
        }

        // POST: Analytics/CreateDelivery
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateDelivery([Bind(Include = "OrderId,LogisticianId,TrackingNumber,CurrentLocation,IsCustomsCleared,Status")] DeliveryInput input)
        {
            if (input.OrderId.HasValue && ModelState.IsValidField("OrderId"))
            {
                if (!Queries.OrderExists(input.OrderId.Value))
                    ModelState.AddModelError("OrderId", "Заявку з таким номером не знайдено");
                else if (AnalyticsDb.Deliveries.Any(d => d.OrderId == input.OrderId.Value))
                    ModelState.AddModelError("OrderId", "Для цієї заявки доставка вже є");
                else if (!IsOrderPaidStatus(input.OrderId.Value))
                    ModelState.AddModelError("OrderId", "Доставку можна створити лише для оплаченої заявки (статус " + PaidOrderStatusNames() + ")");
            }
            ValidateDelivery(input, null);
            if (!ModelState.IsValid) return DeliveryForm(input, true);

            var delivery = new AnalyticsDelivery { OrderId = input.OrderId.Value };
            CopyDelivery(input, delivery);
            AnalyticsDb.Deliveries.Add(delivery);
            if (!TrySave("Не вдалося додати доставку. Перевірте введені дані.")) return DeliveryForm(input, true);

            TempData["Message"] = "Доставку для заявки №" + delivery.OrderId + " додано.";
            return RedirectToAction("DeliveriesTable");
        }

        // GET: Analytics/EditDelivery/5 (5 — номер заявки)
        [HttpGet]
        public ActionResult EditDelivery(int? id)
        {
            var delivery = id.HasValue ? AnalyticsDb.Deliveries.Find(id.Value) : null;
            if (delivery == null) return NotFoundRedirect("Доставку не знайдено.", "DeliveriesTable");

            return DeliveryForm(new DeliveryInput
            {
                OrderId = delivery.OrderId,
                LogisticianId = delivery.LogisticianId,
                TrackingNumber = delivery.TrackingNumber,
                CurrentLocation = delivery.CurrentLocation,
                IsCustomsCleared = delivery.IsCustomsCleared,
                Status = delivery.Status
            }, false);
        }

        // POST: Analytics/EditDelivery/5 — номер заявки береться з адреси, а не з форми
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditDelivery(int? id, [Bind(Include = "LogisticianId,TrackingNumber,CurrentLocation,IsCustomsCleared,Status")] DeliveryInput input)
        {
            var delivery = id.HasValue ? AnalyticsDb.Deliveries.Find(id.Value) : null;
            if (delivery == null) return NotFoundRedirect("Доставку не знайдено.", "DeliveriesTable");

            input.OrderId = delivery.OrderId;
            ModelState.Remove("OrderId");
            ValidateDelivery(input, delivery);
            if (!ModelState.IsValid) return DeliveryForm(input, false);

            CopyDelivery(input, delivery);
            if (!TrySave("Не вдалося змінити доставку. Перевірте введені дані.")) return DeliveryForm(input, false);

            TempData["Message"] = "Доставку для заявки №" + delivery.OrderId + " змінено.";
            return RedirectToAction("DeliveriesTable");
        }

        // POST: Analytics/DeleteDelivery/5 — видалити можна лише доставку, яку ще не розпочато:
        // перший етап («Очікує перевізника»), митницю не пройдено, заявку не завершено
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDelivery(int? id)
        {
            var delivery = id.HasValue ? AnalyticsDb.Deliveries.Find(id.Value) : null;
            if (delivery == null) return NotFoundRedirect("Доставку не знайдено.", "DeliveriesTable");

            int stage = DeliveryStage(delivery.Status);
            if (stage != 0 || delivery.IsCustomsCleared || IsOrderCompleted(delivery.OrderId))
            {
                bool finished = stage == DeliveryStages.Length - 1 || IsOrderCompleted(delivery.OrderId);
                TempData["Message"] = "Доставку для заявки №" + delivery.OrderId + " не можна видалити: її вже "
                    + (finished ? "завершено" : "розпочато") + " (статус «" + delivery.Status + "»"
                    + (delivery.IsCustomsCleared ? ", митницю пройдено" : "") + ").";
                return RedirectToAction("DeliveriesTable");
            }

            AnalyticsDb.Deliveries.Remove(delivery);
            TempData["Message"] = TrySave("")
                ? "Доставку для заявки №" + id.Value + " видалено."
                : "Доставку для заявки №" + id.Value + " не вдалося видалити.";
            return RedirectToAction("DeliveriesTable");
        }

        private ActionResult DeliveryForm(DeliveryInput input, bool isNew)
        {
            ViewBag.IsNew = isNew;
            if (isNew)
            {
                // Доставку можна додати лише для оплаченої заявки, у якої її ще немає
                var taken = AnalyticsDb.Deliveries.Select(d => d.OrderId).ToList();
                var paidStatuses = PaidOrderStatuses;
                var paid = AnalyticsDb.Orders.Where(o => paidStatuses.Contains(o.Status)).Select(o => o.OrderId).ToList();
                ViewBag.Orders = AnalyticsChoices.Pairs(
                    Queries.OrderChoices().Where(o => !taken.Contains(o.Key) && paid.Contains(o.Key)), input.OrderId);
            }
            else
            {
                ViewBag.OrderText = Queries.OrderChoices().Where(o => o.Key == input.OrderId).Select(o => o.Value).FirstOrDefault();
            }
            ViewBag.Logisticians = AnalyticsChoices.Pairs(UsersByRole(UserRole.Logistician), input.LogisticianId);
            // Новій доставці доступний лише перший етап, наявній — поточний і наступні
            int from = isNew ? 0 : Math.Max(0, DeliveryStage(StoredDeliveryStatus(input.OrderId)));
            int to = isNew ? 0 : DeliveryStages.Length - 1;
            ViewBag.DeliveryStatuses = new SelectList(DeliveryStages.Skip(from).Take(to - from + 1), input.Status);
            return View("DeliveryForm", input);
        }

        private string StoredDeliveryStatus(int? orderId)
        {
            return orderId.HasValue
                ? AnalyticsDb.Deliveries.Where(d => d.OrderId == orderId.Value).Select(d => d.Status).FirstOrDefault()
                : null;
        }

        private void ValidateDelivery(DeliveryInput input, AnalyticsDelivery existing)
        {
            // Статус — лише етап зі списку, і етапи йдуть тільки вперед
            int stage = DeliveryStage(input.Status);
            if (input.Status != null && stage < 0)
                ModelState.AddModelError("Status", "Оберіть статус зі списку");
            else if (existing == null && stage > 0)
                ModelState.AddModelError("Status", "Нова доставка починається зі статусу «" + DeliveryStages[0] + "»");
            else if (existing != null && stage >= 0 && stage < DeliveryStage(existing.Status))
                ModelState.AddModelError("Status", "Статус доставки не можна повернути назад (зараз «" + existing.Status + "»)");

            // Перевізника призначає логіст, тож без логіста цей етап неможливий
            if (stage >= CarrierAssignedStage && !input.LogisticianId.HasValue)
                ModelState.AddModelError("LogisticianId", "Щоб призначити перевізника, оберіть логіста");

            // Митне оформлення: без нього авто не відправити, і скасувати його не можна
            if (stage >= OnTheWayStage && !input.IsCustomsCleared)
                ModelState.AddModelError("IsCustomsCleared", "Щоб відправити авто, позначте, що митницю пройдено");
            if (existing != null && existing.IsCustomsCleared && !input.IsCustomsCleared)
                ModelState.AddModelError("IsCustomsCleared", "Митне оформлення не можна скасувати");

            // Після відправлення номер відстеження вже відомий клієнту, змінити його не можна
            if (existing != null && DeliveryStage(existing.Status) >= OnTheWayStage
                && Clean(input.TrackingNumber) != null && Clean(input.TrackingNumber) != existing.TrackingNumber)
                ModelState.AddModelError("TrackingNumber", "Номер відстеження не можна змінити: авто вже в дорозі");

            if (input.LogisticianId.HasValue && !UserHasRole(input.LogisticianId, UserRole.Logistician))
                ModelState.AddModelError("LogisticianId", "Оберіть користувача з роллю «Логіст»");

            string tracking = Clean(input.TrackingNumber);
            int ownId = existing != null ? existing.OrderId : 0;
            if (tracking != null && ModelState.IsValidField("TrackingNumber")
                && AnalyticsDb.Deliveries.Any(d => d.TrackingNumber == tracking && d.OrderId != ownId))
                ModelState.AddModelError("TrackingNumber", "Доставка з таким номером відстеження вже є");
        }

        // Етапи доставки по порядку
        private static readonly string[] DeliveryStages =
        {
            "Очікує перевізника", "Перевізника призначено", "В дорозі", "Готово до видачі", "Доставлено"
        };
        private const int CarrierAssignedStage = 1; // з цього етапу доставку розпочато
        private const int OnTheWayStage = 2;        // з цього етапу авто вже відправлено

        // Номер етапу (0 — перший) або -1, якщо такого статусу немає
        private static int DeliveryStage(string status)
        {
            string s = Clean(status);
            return Array.FindIndex(DeliveryStages, x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsOrderPaidStatus(int orderId)
        {
            var paidStatuses = PaidOrderStatuses;
            return AnalyticsDb.Orders.Any(o => o.OrderId == orderId && paidStatuses.Contains(o.Status));
        }

        private static void CopyDelivery(DeliveryInput input, AnalyticsDelivery delivery)
        {
            delivery.LogisticianId = input.LogisticianId;
            delivery.TrackingNumber = input.TrackingNumber.Trim();
            delivery.CurrentLocation = Clean(input.CurrentLocation);
            delivery.IsCustomsCleared = input.IsCustomsCleared;
            delivery.Status = DeliveryStages[DeliveryStage(input.Status)]; // назва етапу в точному написанні
        }
    }
}
