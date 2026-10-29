using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ASTCUSA.Models;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // Аналітика менеджера за окремою базою astcusa_analytics.
    // Запити лише читають дані (GET); страховки додаються, змінюються й видаляються
    // тільки через POST з антифорджері-токеном.
    [AllowRoles(UserRole.Manager)]
    public class AnalyticsController : BaseController
    {
        private readonly AnalyticsContext analyticsContext = new AnalyticsContext();
        private AnalyticsQueries queries;

        // Перед першим зверненням база створюється скриптом, якщо її ще немає
        private AnalyticsContext AnalyticsDb
        {
            get
            {
                AnalyticsDatabase.EnsureCreated();
                return analyticsContext;
            }
        }

        private AnalyticsQueries Queries
        {
            get { return queries ?? (queries = new AnalyticsQueries(AnalyticsDb)); }
        }

        // Форму запиту вже надіслано (у рядку запиту є параметри), а не просто відкрито сторінку
        private bool Submitted
        {
            get { return Request.QueryString.Count > 0; }
        }

        // GET: Analytics — перша вкладка розділу
        [HttpGet]
        public ActionResult Index()
        {
            return RedirectToAction("Cars");
        }

        // GET: Analytics/Cars — фільтр авто за маркою, роком і діапазоном ціни
        [HttpGet]
        public ActionResult Cars(CarsPage page)
        {
            if (page.YearFrom.HasValue && page.YearTo.HasValue && page.YearFrom > page.YearTo)
                ModelState.AddModelError("YearTo", "«Рік до» не може бути меншим за «Рік від»");
            if (page.PriceFrom.HasValue && page.PriceTo.HasValue && page.PriceFrom > page.PriceTo)
                ModelState.AddModelError("PriceTo", "«Ціна до» не може бути меншою за «Ціна від»");

            if (ModelState.IsValid)
                page.Cars = Queries.FilterCars(page).ToList();
            return View(page);
        }

        // GET: Analytics/ClientOrders — заявки клієнта за e-mail і статусом
        [HttpGet]
        public ActionResult ClientOrders(ClientOrdersPage page)
        {
            page.ClientEmails = Queries.ClientEmails();

            if (!Submitted)
            {
                ModelState.Clear();
                return View(page);
            }

            if (!string.IsNullOrEmpty(page.Status) && !Enum.IsDefined(typeof(OrderStatus), page.Status))
                ModelState.AddModelError("Status", "Невідомий статус заявки");

            if (ModelState.IsValid)
                page.Rows = Queries.ClientOrders(page).ToList();
            return View(page);
        }

        // GET: Analytics/PaidInvoices — сума й кількість оплачених інвойсів за день / місяць / півріччя
        [HttpGet]
        public ActionResult PaidInvoices(PaidInvoicesPage page)
        {
            if (!Submitted)
            {
                // Перше відкриття сторінки: поточний місяць
                ModelState.Clear();
                page.Period = PeriodType.Month;
                page.Date = DateTime.Today;
            }

            if (page.Period.HasValue && !Enum.IsDefined(typeof(PeriodType), page.Period.Value))
                ModelState.AddModelError("Period", "Невідомий тип періоду");
            if (page.Date.HasValue && (page.Date.Value.Year < 2000 || page.Date.Value.Year > 2100))
                ModelState.AddModelError("Date", "Дата має бути в межах 2000–2100 років");

            if (ModelState.IsValid)
            {
                DateTime start, end;
                PaidInvoicesInput.GetRange(page.Period.Value, page.Date.Value, out start, out end);

                var summary = Queries.PaidSummary(start, end).FirstOrDefault();
                page.Total = summary != null ? summary.Total : 0m;
                page.Count = summary != null ? summary.Count : 0;
                page.Rows = Queries.PaidList(start, end).ToList();
                page.Start = start;
                page.End = end;
                page.Ran = true;
            }
            return View(page);
        }

        // GET: Analytics/Deliveries — доставки з даними заявки, авто, клієнта й логіста
        [HttpGet]
        public ActionResult Deliveries(DeliveriesPage page)
        {
            page.Statuses = Queries.DeliveryStatuses();

            if (!string.IsNullOrEmpty(page.Status) && !page.Statuses.Contains(page.Status))
                ModelState.AddModelError("Status", "Невідомий статус доставки");

            if (ModelState.IsValid)
                page.Rows = Queries.Deliveries(page).ToList();
            return View(page);
        }

        // GET: Analytics/Stats — кількість заявок і ціни авто з групуванням за статусом або маркою
        [HttpGet]
        public ActionResult Stats(StatsPage page)
        {
            if (string.IsNullOrEmpty(page.GroupBy)) page.GroupBy = "status";
            bool byMake = page.GroupBy == "make";

            if (page.GroupBy != "status" && !byMake)
                ModelState.AddModelError("GroupBy", "Оберіть групування: за статусом або за маркою");

            if (ModelState.IsValid)
                page.Rows = (byMake ? Queries.StatsByMake() : Queries.StatsByStatus()).ToList();
            return View(page);
        }

        // ---------- Страховки: додавання, зміна, видалення ----------

        private InsurancesPage BuildInsurancesPage(InsuranceInput input)
        {
            var page = new InsurancesPage
            {
                OrderId = input.OrderId,
                Type = input.Type,
                Price = input.Price,
                Rows = Queries.Insurances().ToList()
            };
            ViewBag.Orders = OrderList(input.OrderId);
            return page;
        }

        private SelectList OrderList(int? selected)
        {
            return new SelectList(Queries.OrderChoices(), "Key", "Value", selected);
        }

        private void ValidateOrder(InsuranceInput input)
        {
            if (input.OrderId.HasValue && ModelState.IsValidField("OrderId") && !Queries.OrderExists(input.OrderId.Value))
                ModelState.AddModelError("OrderId", "Заявку з таким номером не знайдено");
        }

        // GET: Analytics/Insurances
        [HttpGet]
        public ActionResult Insurances()
        {
            return View(BuildInsurancesPage(new InsuranceInput()));
        }

        // POST: Analytics/CreateInsurance — додавання
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateInsurance([Bind(Include = "OrderId,Type,Price")] InsuranceInput input)
        {
            ValidateOrder(input);
            if (!ModelState.IsValid) return View("Insurances", BuildInsurancesPage(input));

            var insurance = new AnalyticsInsurance
            {
                OrderId = input.OrderId.Value,
                Type = input.Type.Trim(),
                Price = input.Price.Value
            };
            AnalyticsDb.Insurances.Add(insurance);
            AnalyticsDb.SaveChanges();
            TempData["Message"] = "Страховку №" + insurance.InsuranceId + " додано.";
            return RedirectToAction("Insurances");
        }

        // GET: Analytics/EditInsurance/5
        [HttpGet]
        public ActionResult EditInsurance(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var insurance = AnalyticsDb.Insurances.Find(id.Value);
            if (insurance == null)
            {
                TempData["Message"] = "Страховку не знайдено.";
                return RedirectToAction("Insurances");
            }
            ViewBag.Orders = OrderList(insurance.OrderId);
            return View(new InsuranceInput
            {
                InsuranceId = insurance.InsuranceId,
                OrderId = insurance.OrderId,
                Type = insurance.Type,
                Price = insurance.Price
            });
        }

        // POST: Analytics/EditInsurance/5 — зміна
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditInsurance([Bind(Include = "InsuranceId,OrderId,Type,Price")] InsuranceInput input)
        {
            var insurance = input.InsuranceId.HasValue ? AnalyticsDb.Insurances.Find(input.InsuranceId.Value) : null;
            if (insurance == null)
            {
                TempData["Message"] = "Страховку не знайдено.";
                return RedirectToAction("Insurances");
            }

            ValidateOrder(input);
            if (!ModelState.IsValid)
            {
                ViewBag.Orders = OrderList(input.OrderId);
                return View(input);
            }

            insurance.OrderId = input.OrderId.Value;
            insurance.Type = input.Type.Trim();
            insurance.Price = input.Price.Value;
            AnalyticsDb.SaveChanges();
            TempData["Message"] = "Страховку №" + insurance.InsuranceId + " змінено.";
            return RedirectToAction("Insurances");
        }

        // POST: Analytics/DeleteInsurance/5 — видалення
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteInsurance(int? id)
        {
            var insurance = id.HasValue ? AnalyticsDb.Insurances.Find(id.Value) : null;
            if (insurance == null)
            {
                TempData["Message"] = "Страховку не знайдено.";
            }
            else
            {
                AnalyticsDb.Insurances.Remove(insurance);
                AnalyticsDb.SaveChanges();
                TempData["Message"] = "Страховку №" + id.Value + " видалено.";
            }
            return RedirectToAction("Insurances");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) analyticsContext.Dispose();
            base.Dispose(disposing);
        }
    }
}
