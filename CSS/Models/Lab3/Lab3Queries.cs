using System;
using System.Collections.Generic;
using System.Linq;

namespace ASTCUSA.Models.Lab3
{
    // LINQ-запити лабораторної №3 до бази astcusa_lab3.
    // Поруч із кожним запитом — його текст (константа *Linq), який показується на сторінці.
    // Тексти збігаються з кодом методів нижче; змінюючи запит, змініть і текст.
    public class Lab3Queries
    {
        private readonly Lab3Context db;

        public Lab3Queries(Lab3Context db)
        {
            this.db = db;
        }

        // ---------- 0. Кількість рядків у таблицях ----------

        public const string TableCountsLinq =
@"db.Users.Count();  db.Cars.Count();      db.Orders.Count();
db.Insurances.Count();  db.Invoices.Count();  db.Deliveries.Count();";

        public IList<TableCountRow> TableCounts()
        {
            return new List<TableCountRow>
            {
                new TableCountRow { Table = "Users", Entity = "User (Client / Manager / Logistician)", Count = db.Users.Count() },
                new TableCountRow { Table = "Cars", Entity = "Car", Count = db.Cars.Count() },
                new TableCountRow { Table = "Orders", Entity = "Order", Count = db.Orders.Count() },
                new TableCountRow { Table = "Insurances", Entity = "Insurance", Count = db.Insurances.Count() },
                new TableCountRow { Table = "Invoices", Entity = "Invoice", Count = db.Invoices.Count() },
                new TableCountRow { Table = "Deliveries", Entity = "Delivery", Count = db.Deliveries.Count() }
            };
        }

        // ---------- 1. Фільтр авто за маркою, роком і ціною ----------

        public const string CarsLinq =
@"string make = (filter.Make ?? """").Trim();
int? yearFrom = filter.YearFrom, yearTo = filter.YearTo;
decimal? priceFrom = filter.PriceFrom, priceTo = filter.PriceTo;

var query = from c in db.Cars
            where (make == """" || c.Make.Contains(make))
               && (yearFrom == null || c.Year >= yearFrom)
               && (yearTo == null || c.Year <= yearTo)
               && (priceFrom == null || c.AuctionPrice >= priceFrom)
               && (priceTo == null || c.AuctionPrice <= priceTo)
            orderby c.Make, c.Model, c.Year
            select c;";

        public IQueryable<Lab3Car> FilterCars(CarFilterInput filter)
        {
            string make = (filter.Make ?? "").Trim();
            int? yearFrom = filter.YearFrom, yearTo = filter.YearTo;
            decimal? priceFrom = filter.PriceFrom, priceTo = filter.PriceTo;

            var query = from c in db.Cars
                        where (make == "" || c.Make.Contains(make))
                           && (yearFrom == null || c.Year >= yearFrom)
                           && (yearTo == null || c.Year <= yearTo)
                           && (priceFrom == null || c.AuctionPrice >= priceFrom)
                           && (priceTo == null || c.AuctionPrice <= priceTo)
                        orderby c.Make, c.Model, c.Year
                        select c;
            return query;
        }

        // ---------- 2. Заявки клієнта за e-mail і статусом ----------

        public const string ClientOrdersLinq =
@"string email = input.Email.Trim().ToLowerInvariant();
string status = input.Status ?? """";

var query = from u in db.Users
            join o in db.Orders on u.Id equals o.ClientId
            join c in db.Cars on o.CarId equals (int?)c.CarId
            join m in db.Users on o.ManagerId equals (int?)m.Id into managers
            from m in managers.DefaultIfEmpty()          // LEFT JOIN: менеджера може не бути
            where u.Email == email && (status == """" || o.Status == status)
            orderby o.CreatedAt descending
            select new ClientOrderRow
            {
                OrderId = o.OrderId, ClientName = u.Name, CreatedAt = o.CreatedAt,
                Status = o.Status, Make = c.Make, Model = c.Model, Year = c.Year,
                MaxBudget = o.MaxBudget, FinalPrice = o.FinalPrice, ManagerName = m.Name
            };";

        public IQueryable<ClientOrderRow> ClientOrders(ClientOrdersInput input)
        {
            string email = input.Email.Trim().ToLowerInvariant();
            string status = input.Status ?? "";

            var query = from u in db.Users
                        join o in db.Orders on u.Id equals o.ClientId
                        join c in db.Cars on o.CarId equals (int?)c.CarId
                        join m in db.Users on o.ManagerId equals (int?)m.Id into managers
                        from m in managers.DefaultIfEmpty()
                        where u.Email == email && (status == "" || o.Status == status)
                        orderby o.CreatedAt descending
                        select new ClientOrderRow
                        {
                            OrderId = o.OrderId, ClientName = u.Name, CreatedAt = o.CreatedAt,
                            Status = o.Status, Make = c.Make, Model = c.Model, Year = c.Year,
                            MaxBudget = o.MaxBudget, FinalPrice = o.FinalPrice, ManagerName = m.Name
                        };
            return query;
        }

        public IList<string> ClientEmails()
        {
            return db.Users.Where(u => u.Role == "Client").OrderBy(u => u.Email).Select(u => u.Email).ToList();
        }

        // ---------- 3. Сума оплачених інвойсів за період ----------

        public const string PaidSummaryLinq =
@"DateTime start, end;   // день / місяць / півріччя, що містить вибрану дату
PaidInvoicesInput.GetRange(period, date, out start, out end);

var query = from i in db.Invoices
            where i.IsPaid && i.PaidAt >= start && i.PaidAt < end
            group i by i.IsPaid into g
            select new PaidInvoiceSummary
            {
                Total = g.Sum(x => x.TotalAmount),   // узагальнення: сума
                Count = g.Count()                    // і кількість
            };
var summary = query.FirstOrDefault();   // null — оплат у періоді немає";

        public IQueryable<PaidInvoiceSummary> PaidSummary(DateTime start, DateTime end)
        {
            var query = from i in db.Invoices
                        where i.IsPaid && i.PaidAt >= start && i.PaidAt < end
                        group i by i.IsPaid into g
                        select new PaidInvoiceSummary
                        {
                            Total = g.Sum(x => x.TotalAmount),
                            Count = g.Count()
                        };
            return query;
        }

        public const string PaidListLinq =
@"var query = from i in db.Invoices
            join o in db.Orders on i.OrderId equals o.OrderId
            join u in db.Users on o.ClientId equals u.Id
            where i.IsPaid && i.PaidAt >= start && i.PaidAt < end
            orderby i.PaidAt
            select new PaidInvoiceRow
            {
                InvoiceId = i.InvoiceId, OrderId = o.OrderId, ClientName = u.Name,
                TotalAmount = i.TotalAmount, PaidAt = i.PaidAt
            };";

        public IQueryable<PaidInvoiceRow> PaidList(DateTime start, DateTime end)
        {
            var query = from i in db.Invoices
                        join o in db.Orders on i.OrderId equals o.OrderId
                        join u in db.Users on o.ClientId equals u.Id
                        where i.IsPaid && i.PaidAt >= start && i.PaidAt < end
                        orderby i.PaidAt
                        select new PaidInvoiceRow
                        {
                            InvoiceId = i.InvoiceId, OrderId = o.OrderId, ClientName = u.Name,
                            TotalAmount = i.TotalAmount, PaidAt = i.PaidAt
                        };
            return query;
        }

        // ---------- 4. Доставки з даними заявки, авто, клієнта й логіста ----------

        public const string DeliveriesLinq =
@"string status = input.Status ?? """";
bool? customs = input.CustomsCleared;

var query = from d in db.Deliveries
            join o in db.Orders on d.OrderId equals o.OrderId
            join c in db.Cars on o.CarId equals (int?)c.CarId
            join cl in db.Users on o.ClientId equals cl.Id
            join l in db.Users on d.LogisticianId equals (int?)l.Id into logisticians
            from l in logisticians.DefaultIfEmpty()      // LEFT JOIN: логіста може не бути
            where (status == """" || d.Status == status)
               && (customs == null || d.IsCustomsCleared == customs)
            orderby d.OrderId
            select new DeliveryRow
            {
                OrderId = o.OrderId, TrackingNumber = d.TrackingNumber, Status = d.Status,
                CurrentLocation = d.CurrentLocation, IsCustomsCleared = d.IsCustomsCleared,
                OrderStatus = o.Status, ClientName = cl.Name, Make = c.Make, Model = c.Model,
                VinCode = c.VinCode, LogisticianName = l.Name
            };";

        public IQueryable<DeliveryRow> Deliveries(DeliveriesInput input)
        {
            string status = input.Status ?? "";
            bool? customs = input.CustomsCleared;

            var query = from d in db.Deliveries
                        join o in db.Orders on d.OrderId equals o.OrderId
                        join c in db.Cars on o.CarId equals (int?)c.CarId
                        join cl in db.Users on o.ClientId equals cl.Id
                        join l in db.Users on d.LogisticianId equals (int?)l.Id into logisticians
                        from l in logisticians.DefaultIfEmpty()
                        where (status == "" || d.Status == status)
                           && (customs == null || d.IsCustomsCleared == customs)
                        orderby d.OrderId
                        select new DeliveryRow
                        {
                            OrderId = o.OrderId, TrackingNumber = d.TrackingNumber, Status = d.Status,
                            CurrentLocation = d.CurrentLocation, IsCustomsCleared = d.IsCustomsCleared,
                            OrderStatus = o.Status, ClientName = cl.Name, Make = c.Make, Model = c.Model,
                            VinCode = c.VinCode, LogisticianName = l.Name
                        };
            return query;
        }

        public IList<string> DeliveryStatuses()
        {
            return db.Deliveries.Select(d => d.Status).Distinct().OrderBy(s => s).ToList();
        }

        // ---------- 5. Групування заявок за статусом або маркою ----------

        public const string StatsByStatusLinq =
@"var query = from o in db.Orders
            join c in db.Cars on o.CarId equals (int?)c.CarId
            group c by o.Status into g
            orderby g.Count() descending, g.Key
            select new GroupRow
            {
                Key = g.Key,
                OrdersCount = g.Count(),
                AvgCarPrice = g.Average(x => x.AuctionPrice),
                MinCarPrice = g.Min(x => x.AuctionPrice),
                MaxCarPrice = g.Max(x => x.AuctionPrice)
            };";

        public const string StatsByMakeLinq =
@"var query = from o in db.Orders
            join c in db.Cars on o.CarId equals (int?)c.CarId
            group c by c.Make into g
            orderby g.Count() descending, g.Key
            select new GroupRow
            {
                Key = g.Key,
                OrdersCount = g.Count(),
                AvgCarPrice = g.Average(x => x.AuctionPrice),
                MinCarPrice = g.Min(x => x.AuctionPrice),
                MaxCarPrice = g.Max(x => x.AuctionPrice)
            };";

        public IQueryable<GroupRow> StatsByStatus()
        {
            var query = from o in db.Orders
                        join c in db.Cars on o.CarId equals (int?)c.CarId
                        group c by o.Status into g
                        orderby g.Count() descending, g.Key
                        select new GroupRow
                        {
                            Key = g.Key,
                            OrdersCount = g.Count(),
                            AvgCarPrice = g.Average(x => x.AuctionPrice),
                            MinCarPrice = g.Min(x => x.AuctionPrice),
                            MaxCarPrice = g.Max(x => x.AuctionPrice)
                        };
            return query;
        }

        public IQueryable<GroupRow> StatsByMake()
        {
            var query = from o in db.Orders
                        join c in db.Cars on o.CarId equals (int?)c.CarId
                        group c by c.Make into g
                        orderby g.Count() descending, g.Key
                        select new GroupRow
                        {
                            Key = g.Key,
                            OrdersCount = g.Count(),
                            AvgCarPrice = g.Average(x => x.AuctionPrice),
                            MinCarPrice = g.Min(x => x.AuctionPrice),
                            MaxCarPrice = g.Max(x => x.AuctionPrice)
                        };
            return query;
        }

        // ---------- 6. Страховки: перелік і зміна даних ----------

        public const string InsurancesLinq =
@"var query = from i in db.Insurances
            join o in db.Orders on i.OrderId equals o.OrderId
            join u in db.Users on o.ClientId equals u.Id
            orderby i.InsuranceId descending
            select new InsuranceRow
            {
                InsuranceId = i.InsuranceId, OrderId = o.OrderId, OrderStatus = o.Status,
                ClientName = u.Name, Type = i.Type, Price = i.Price
            };";

        public IQueryable<InsuranceRow> Insurances()
        {
            var query = from i in db.Insurances
                        join o in db.Orders on i.OrderId equals o.OrderId
                        join u in db.Users on o.ClientId equals u.Id
                        orderby i.InsuranceId descending
                        select new InsuranceRow
                        {
                            InsuranceId = i.InsuranceId, OrderId = o.OrderId, OrderStatus = o.Status,
                            ClientName = u.Name, Type = i.Type, Price = i.Price
                        };
            return query;
        }

        public const string InsuranceCrudLinq =
@"// Перевірка: заявка існує
bool orderExists = db.Orders.Any(o => o.OrderId == input.OrderId);

// Додавання (INSERT)
db.Insurances.Add(new Lab3Insurance { OrderId = orderId, Type = type, Price = price });
db.SaveChanges();

// Зміна (UPDATE)
var insurance = db.Insurances.Find(id);
insurance.OrderId = orderId; insurance.Type = type; insurance.Price = price;
db.SaveChanges();

// Видалення (DELETE)
db.Insurances.Remove(db.Insurances.Find(id));
db.SaveChanges();";

        public bool OrderExists(int orderId)
        {
            return db.Orders.Any(o => o.OrderId == orderId);
        }

        // Заявки для списку вибору у формі страховки
        public IList<KeyValuePair<int, string>> OrderChoices()
        {
            var rows = (from o in db.Orders
                        join u in db.Users on o.ClientId equals u.Id
                        join c in db.Cars on o.CarId equals (int?)c.CarId into cars
                        from c in cars.DefaultIfEmpty()
                        orderby o.OrderId
                        select new { o.OrderId, u.Name, c.Make, c.Model, o.Status }).ToList();
            return rows.Select(r => new KeyValuePair<int, string>(r.OrderId, string.Format("№{0} — {1}, {2} {3} ({4})",
                r.OrderId, r.Name, r.Make, r.Model, Lab3Text.OrderStatusName(r.Status)))).ToList();
        }
    }
}
