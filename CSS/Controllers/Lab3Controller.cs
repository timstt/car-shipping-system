using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ASTCUSA.Models;
using ASTCUSA.Models.Lab3;

namespace ASTCUSA.Controllers
{
    // Лабораторна №3: окрема локальна база astcusa_lab3 і LINQ-запити до неї.
    // Запити лише читають дані (GET); страховки додаються, змінюються й видаляються
    // тільки через POST з антифорджері-токеном. Сторінки доступні лише менеджеру.
    [AllowRoles(UserRole.Manager)]
    public class Lab3Controller : BaseController
    {
        private readonly Lab3Context lab3 = new Lab3Context();
        private Lab3Queries queries;

        // Перед першим зверненням база створюється скриптом, якщо її ще немає
        private Lab3Context Db3
        {
            get
            {
                Lab3Database.EnsureCreated();
                return lab3;
            }
        }

        private Lab3Queries Queries
        {
            get { return queries ?? (queries = new Lab3Queries(Db3)); }
        }

        // Форму запиту вже надіслано (у рядку запиту є параметри), а не просто відкрито сторінку
        private bool Submitted
        {
            get { return Request.QueryString.Count > 0; }
        }

        private static QueryInfo Info(string title, string description, string linq, params string[] kinds)
        {
            return new QueryInfo { Title = title, Description = description, Linq = linq, Kinds = kinds };
        }

        // GET: Lab3 — опис бази і кількість рядків у таблицях
        [HttpGet]
        public ActionResult Index()
        {
            var page = new Lab3IndexPage
            {
                Info = Info("Кількість рядків у таблицях",
                    "Count() для кожної з шести таблиць бази.",
                    Lab3Queries.TableCountsLinq, "Узагальнення (Count)"),
                Tables = Queries.TableCounts(),
                Server = Lab3Database.ServerName,
                Database = Lab3Database.DatabaseName
            };
            return View(page);
        }

        // GET: Lab3/Cars — 1. Фільтр авто за маркою, роком і діапазоном ціни
        [HttpGet]
        public ActionResult Cars(CarsPage page)
        {
            page.Info = Info("1. Фільтр авто за маркою, роком і ціною",
                "Відбір авто за умовами, які вводить користувач. Порожні поля не обмежують результат.",
                Lab3Queries.CarsLinq, "Фільтрація (where)", "Сортування (orderby)");

            if (page.YearFrom.HasValue && page.YearTo.HasValue && page.YearFrom > page.YearTo)
                ModelState.AddModelError("YearTo", "«Рік до» не може бути меншим за «Рік від»");
            if (page.PriceFrom.HasValue && page.PriceTo.HasValue && page.PriceFrom > page.PriceTo)
                ModelState.AddModelError("PriceTo", "«Ціна до» не може бути меншою за «Ціна від»");

            if (ModelState.IsValid)
            {
                var query = Queries.FilterCars(page);
                page.Info.Sql = query.ToString();
                page.Cars = query.ToList();
            }
            return View(page);
        }

        // GET: Lab3/ClientOrders — 2. Заявки клієнта за e-mail і статусом
        [HttpGet]
        public ActionResult ClientOrders(ClientOrdersPage page)
        {
            page.Info = Info("2. Заявки клієнта за e-mail і статусом",
                "Пошук клієнта за e-mail і його заявок з даними авто та менеджера; статус заявки — необов'язковий фільтр.",
                Lab3Queries.ClientOrdersLinq, "Фільтрація (where)", "З'єднання Users–Orders–Cars–Users (join, left join)");
            page.ClientEmails = Queries.ClientEmails();

            if (!Submitted)
            {
                ModelState.Clear();
                return View(page);
            }

            page.Ran = true;
            if (!string.IsNullOrEmpty(page.Status) && !Enum.IsDefined(typeof(OrderStatus), page.Status))
                ModelState.AddModelError("Status", "Невідомий статус заявки");

            if (ModelState.IsValid)
            {
                var query = Queries.ClientOrders(page);
                page.Info.Sql = query.ToString();
                page.Rows = query.ToList();
            }
            return View(page);
        }

        // GET: Lab3/PaidInvoices — 3. Сума й кількість оплачених інвойсів за день / місяць / півріччя
        [HttpGet]
        public ActionResult PaidInvoices(PaidInvoicesPage page)
        {
            page.SummaryInfo = Info("3. Сума оплачених інвойсів за період",
                "Користувач вибирає тип періоду (день, місяць або півріччя) і дату всередині нього; запит рахує суму й кількість оплат.",
                Lab3Queries.PaidSummaryLinq, "Узагальнення (group, Sum, Count)", "Фільтрація за датою");
            page.ListInfo = Info("Оплачені інвойси за цей період",
                "Перелік оплат, з яких складається сума: інвойс, заявка і клієнт.",
                Lab3Queries.PaidListLinq, "З'єднання Invoices–Orders–Users (join)");

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

                var summaryQuery = Queries.PaidSummary(start, end);
                var listQuery = Queries.PaidList(start, end);
                page.SummaryInfo.Sql = summaryQuery.ToString();
                page.ListInfo.Sql = listQuery.ToString();

                var summary = summaryQuery.FirstOrDefault();
                page.Total = summary != null ? summary.Total : 0m;
                page.Count = summary != null ? summary.Count : 0;
                page.Rows = listQuery.ToList();
                page.Start = start;
                page.End = end;
                page.Ran = true;
            }
            return View(page);
        }

        // GET: Lab3/Deliveries — 4. Доставки з даними заявки, авто, клієнта й логіста
        [HttpGet]
        public ActionResult Deliveries(DeliveriesPage page)
        {
            page.Info = Info("4. Доставки за статусом",
                "Доставки разом із заявкою, авто, клієнтом і логістом; фільтр за статусом доставки й митницею.",
                Lab3Queries.DeliveriesLinq, "Фільтрація (where)", "З'єднання п'яти таблиць (join, left join)");
            page.Statuses = Queries.DeliveryStatuses();

            if (!string.IsNullOrEmpty(page.Status) && !page.Statuses.Contains(page.Status))
                ModelState.AddModelError("Status", "Невідомий статус доставки");

            if (ModelState.IsValid)
            {
                var query = Queries.Deliveries(page);
                page.Info.Sql = query.ToString();
                page.Rows = query.ToList();
            }
            return View(page);
        }

        // GET: Lab3/Stats — 5. Кількість заявок і ціни авто з групуванням за статусом або маркою
        [HttpGet]
        public ActionResult Stats(StatsPage page)
        {
            if (string.IsNullOrEmpty(page.GroupBy)) page.GroupBy = "status";
            bool byMake = page.GroupBy == "make";

            page.Info = Info("5. Групування заявок " + (byMake ? "за маркою авто" : "за статусом"),
                "Кількість заявок, середня, мінімальна й максимальна ціна авто в кожній групі.",
                byMake ? Lab3Queries.StatsByMakeLinq : Lab3Queries.StatsByStatusLinq,
                "Групування (group by)", "Узагальнення (Count, Average, Min, Max)", "З'єднання Orders–Cars (join)");

            if (page.GroupBy != "status" && !byMake)
                ModelState.AddModelError("GroupBy", "Оберіть групування: за статусом або за маркою");

            if (ModelState.IsValid)
            {
                var query = byMake ? Queries.StatsByMake() : Queries.StatsByStatus();
                page.Info.Sql = query.ToString();
                page.Rows = query.ToList();
            }
            return View(page);
        }

        // ---------- 6. Страховки: додавання, зміна, видалення ----------

        private InsurancesPage BuildInsurancesPage(InsuranceInput input)
        {
            var query = Queries.Insurances();
            var page = new InsurancesPage
            {
                OrderId = input.OrderId,
                Type = input.Type,
                Price = input.Price,
                ListInfo = Info("6. Страховки: перелік",
                    "Усі страховки з номером заявки, її статусом і клієнтом.",
                    Lab3Queries.InsurancesLinq, "З'єднання Insurances–Orders–Users (join)"),
                CrudInfo = Info("6. Страховки: додавання, зміна й видалення",
                    "Зміна даних у таблиці Insurances через Lab3Context з перевіркою введених значень.",
                    Lab3Queries.InsuranceCrudLinq, "Додавання (Add)", "Зміна (оновлення полів)", "Видалення (Remove)")
            };
            page.ListInfo.Sql = query.ToString();
            page.Rows = query.ToList();
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

        // GET: Lab3/Insurances
        [HttpGet]
        public ActionResult Insurances()
        {
            return View(BuildInsurancesPage(new InsuranceInput()));
        }

        // POST: Lab3/CreateInsurance — додавання
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateInsurance([Bind(Include = "OrderId,Type,Price")] InsuranceInput input)
        {
            ValidateOrder(input);
            if (!ModelState.IsValid) return View("Insurances", BuildInsurancesPage(input));

            var insurance = new Lab3Insurance
            {
                OrderId = input.OrderId.Value,
                Type = input.Type.Trim(),
                Price = input.Price.Value
            };
            Db3.Insurances.Add(insurance);
            Db3.SaveChanges();
            TempData["Message"] = "Страховку №" + insurance.InsuranceId + " додано.";
            return RedirectToAction("Insurances");
        }

        // GET: Lab3/EditInsurance/5
        [HttpGet]
        public ActionResult EditInsurance(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var insurance = Db3.Insurances.Find(id.Value);
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

        // POST: Lab3/EditInsurance/5 — зміна
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditInsurance([Bind(Include = "InsuranceId,OrderId,Type,Price")] InsuranceInput input)
        {
            var insurance = input.InsuranceId.HasValue ? Db3.Insurances.Find(input.InsuranceId.Value) : null;
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
            Db3.SaveChanges();
            TempData["Message"] = "Страховку №" + insurance.InsuranceId + " змінено.";
            return RedirectToAction("Insurances");
        }

        // POST: Lab3/DeleteInsurance/5 — видалення
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteInsurance(int? id)
        {
            var insurance = id.HasValue ? Db3.Insurances.Find(id.Value) : null;
            if (insurance == null)
            {
                TempData["Message"] = "Страховку не знайдено.";
            }
            else
            {
                Db3.Insurances.Remove(insurance);
                Db3.SaveChanges();
                TempData["Message"] = "Страховку №" + id.Value + " видалено.";
            }
            return RedirectToAction("Insurances");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) lab3.Dispose();
            base.Dispose(disposing);
        }
    }
}
