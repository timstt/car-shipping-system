using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ASTCUSA.Models
{
    // «Сформувати звітність»: зведення заявок за період і статусами
    public class OrderReport
    {
        public DateTime From { get; private set; }
        public DateTime To { get; private set; }
        public IList<Order> Orders { get; private set; }

        public OrderReport(DateTime from, DateTime to, IEnumerable<Order> orders)
        {
            From = from.Date;
            To = to.Date;
            Orders = orders.OrderBy(o => o.CreatedAt).ToList();
        }

        public IDictionary<OrderStatus, int> CountByStatus
        {
            get
            {
                return Orders.GroupBy(o => o.Status)
                             .OrderBy(g => g.Key)
                             .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        private static readonly OrderStatus[] PaidStatuses =
            { OrderStatus.Paid, OrderStatus.Shipping, OrderStatus.Completed };

        public int PaidCount { get { return Orders.Count(o => PaidStatuses.Contains(o.Status)); } }

        // Сума оплачених заявок (за виграною ставкою)
        public double PaidTotal
        {
            get { return Orders.Where(o => PaidStatuses.Contains(o.Status)).Sum(o => o.FinalPrice ?? 0); }
        }

        // «Експортувати звіт»: CSV з роздільником «;» і BOM, щоб Excel правильно відкрив кирилицю
        public byte[] ToCsv()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("Звіт за період;{0:dd.MM.yyyy};{1:dd.MM.yyyy}", From, To));
            sb.AppendLine("№;Дата;Клієнт;Менеджер;Авто;Бюджет;Фінальна ціна;Статус");
            foreach (var o in Orders)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    o.OrderId.ToString(inv),
                    o.CreatedAt.ToString("dd.MM.yyyy", inv),
                    Csv(o.Client != null ? o.Client.Name : ""),
                    Csv(o.Manager != null ? o.Manager.Name : ""),
                    Csv(o.Car != null ? o.Car.Make + " " + o.Car.Model + " " + o.Car.VinCode : ""),
                    o.MaxBudget.ToString("0.##", inv),
                    o.FinalPrice.HasValue ? o.FinalPrice.Value.ToString("0.##", inv) : "",
                    Csv(o.Status.GetDisplayName())
                }));
            }
            sb.AppendLine();
            sb.AppendLine("Статус;Кількість");
            foreach (var pair in CountByStatus)
                sb.AppendLine(Csv(pair.Key.GetDisplayName()) + ";" + pair.Value.ToString(inv));
            sb.AppendLine("Оплачено заявок;" + PaidCount.ToString(inv));
            sb.AppendLine("Сума оплачених;" + PaidTotal.ToString("0.##", inv));

            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            return preamble.Concat(body).ToArray();
        }

        private static string Csv(string value)
        {
            value = value ?? "";
            if (value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
