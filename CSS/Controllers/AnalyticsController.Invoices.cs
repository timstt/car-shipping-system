using System;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // Таблиця Invoices: перегляд, додавання, зміна, видалення.
    // Одна заявка — щонайбільше один інвойс; «оплачено» ⇔ є дата оплати (CK_Invoices_PaidAt).
    public partial class AnalyticsController
    {
        // GET: Analytics/InvoicesTable
        [HttpGet]
        public ActionResult InvoicesTable()
        {
            var rows = (from i in AnalyticsDb.Invoices
                        join o in AnalyticsDb.Orders on i.OrderId equals o.OrderId
                        join u in AnalyticsDb.Users on o.ClientId equals u.Id
                        orderby i.InvoiceId
                        select new InvoiceTableRow
                        {
                            InvoiceId = i.InvoiceId,
                            OrderId = i.OrderId,
                            ClientName = u.Name,
                            TotalAmount = i.TotalAmount,
                            IsPaid = i.IsPaid,
                            PaidAt = i.PaidAt
                        }).ToList();
            return View(rows);
        }

        // GET: Analytics/CreateInvoice
        [HttpGet]
        public ActionResult CreateInvoice()
        {
            return InvoiceForm(new InvoiceInput());
        }

        // POST: Analytics/CreateInvoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateInvoice([Bind(Include = "OrderId,TotalAmount,IsPaid,PaidAt")] InvoiceInput input)
        {
            ValidateInvoice(input, null);
            if (!ModelState.IsValid) return InvoiceForm(input);

            var invoice = new AnalyticsInvoice();
            CopyInvoice(input, invoice);
            AnalyticsDb.Invoices.Add(invoice);
            if (!TrySave("Не вдалося додати інвойс. Перевірте введені дані.")) return InvoiceForm(input);

            TempData["Message"] = "Інвойс №" + invoice.InvoiceId + " додано.";
            return RedirectToAction("InvoicesTable");
        }

        // GET: Analytics/EditInvoice/5
        [HttpGet]
        public ActionResult EditInvoice(int? id)
        {
            var invoice = id.HasValue ? AnalyticsDb.Invoices.Find(id.Value) : null;
            if (invoice == null) return NotFoundRedirect("Інвойс не знайдено.", "InvoicesTable");

            return InvoiceForm(new InvoiceInput
            {
                InvoiceId = invoice.InvoiceId,
                OrderId = invoice.OrderId,
                TotalAmount = invoice.TotalAmount,
                IsPaid = invoice.IsPaid,
                PaidAt = invoice.PaidAt
            });
        }

        // POST: Analytics/EditInvoice/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditInvoice([Bind(Include = "InvoiceId,OrderId,TotalAmount,IsPaid,PaidAt")] InvoiceInput input)
        {
            var invoice = input.InvoiceId.HasValue ? AnalyticsDb.Invoices.Find(input.InvoiceId.Value) : null;
            if (invoice == null) return NotFoundRedirect("Інвойс не знайдено.", "InvoicesTable");

            ValidateInvoice(input, invoice);
            if (!ModelState.IsValid) return InvoiceForm(input);

            CopyInvoice(input, invoice);
            if (!TrySave("Не вдалося змінити інвойс. Перевірте введені дані.")) return InvoiceForm(input);

            TempData["Message"] = "Інвойс №" + invoice.InvoiceId + " змінено.";
            return RedirectToAction("InvoicesTable");
        }

        // POST: Analytics/DeleteInvoice/5 — оплачений інвойс (запис про отримані гроші) видалити не можна
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteInvoice(int? id)
        {
            var invoice = id.HasValue ? AnalyticsDb.Invoices.Find(id.Value) : null;
            if (invoice == null) return NotFoundRedirect("Інвойс не знайдено.", "InvoicesTable");

            if (invoice.IsPaid)
            {
                TempData["Message"] = "Інвойс №" + invoice.InvoiceId + " не можна видалити: його вже оплачено.";
                return RedirectToAction("InvoicesTable");
            }

            AnalyticsDb.Invoices.Remove(invoice);
            TempData["Message"] = TrySave("")
                ? "Інвойс №" + id.Value + " видалено."
                : "Інвойс №" + id.Value + " не вдалося видалити.";
            return RedirectToAction("InvoicesTable");
        }

        private ActionResult InvoiceForm(InvoiceInput input)
        {
            // У списку — заявки без інвойсу та (при зміні) заявка цього інвойсу
            int? own = input.InvoiceId.HasValue
                ? AnalyticsDb.Invoices.Where(i => i.InvoiceId == input.InvoiceId.Value).Select(i => (int?)i.OrderId).FirstOrDefault()
                : null;
            var taken = AnalyticsDb.Invoices.Select(i => i.OrderId).ToList();
            var orders = Queries.OrderChoices().Where(o => o.Key == own || !taken.Contains(o.Key));
            ViewBag.Orders = AnalyticsChoices.Pairs(orders, input.OrderId);
            return View("InvoiceForm", input);
        }

        private void ValidateInvoice(InvoiceInput input, AnalyticsInvoice existing)
        {
            if (input.OrderId.HasValue && ModelState.IsValidField("OrderId"))
            {
                int orderId = input.OrderId.Value;
                int ownId = existing != null ? existing.InvoiceId : 0;
                if (!Queries.OrderExists(orderId))
                    ModelState.AddModelError("OrderId", "Заявку з таким номером не знайдено");
                else if (AnalyticsDb.Invoices.Any(i => i.OrderId == orderId && i.InvoiceId != ownId))
                    ModelState.AddModelError("OrderId", "Для цієї заявки інвойс уже є");
            }

            // Оплачений інвойс не можна «розоплатити», перенести на іншу заявку чи змінити суму,
            // інакше обмеження на видалення можна було б обійти через редагування
            if (existing != null && existing.IsPaid)
            {
                if (!input.IsPaid)
                    ModelState.AddModelError("IsPaid", "Оплату не можна скасувати: інвойс уже оплачено");
                if (input.OrderId.HasValue && input.OrderId.Value != existing.OrderId)
                    ModelState.AddModelError("OrderId", "Заявку оплаченого інвойсу змінити не можна");
                if (input.TotalAmount.HasValue && input.TotalAmount.Value != existing.TotalAmount)
                    ModelState.AddModelError("TotalAmount", "Суму оплаченого інвойсу змінити не можна");
            }

            if (input.IsPaid && !input.PaidAt.HasValue && ModelState.IsValidField("PaidAt"))
                ModelState.AddModelError("PaidAt", "Для оплаченого інвойсу вкажіть дату оплати");
            if (input.IsPaid && input.PaidAt.HasValue && (input.PaidAt.Value.Year < 2000 || input.PaidAt.Value.Year > 2100))
                ModelState.AddModelError("PaidAt", "Дата має бути в межах 2000–2100 років");
        }

        private static void CopyInvoice(InvoiceInput input, AnalyticsInvoice invoice)
        {
            invoice.OrderId = input.OrderId.Value;
            invoice.TotalAmount = input.TotalAmount.Value;
            invoice.IsPaid = input.IsPaid;
            if (!input.IsPaid)
                invoice.PaidAt = null; // неоплачений інвойс не має дати оплати
            else if (!invoice.PaidAt.HasValue || invoice.PaidAt.Value.Date != input.PaidAt.Value.Date)
                invoice.PaidAt = input.PaidAt.Value.Date;
        }
    }
}
