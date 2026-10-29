using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASTCUSA.Models.Lab3
{
    // ---------- Фільтр авто ----------

    public class CarFilterInput
    {
        [StringLength(50, ErrorMessage = "Марка задовга (до 50 символів)")]
        [Display(Name = "Марка")]
        public string Make { get; set; }

        [Range(1950, 2100, ErrorMessage = "Рік має бути від 1950 до 2100")]
        [Display(Name = "Рік від")]
        public int? YearFrom { get; set; }

        [Range(1950, 2100, ErrorMessage = "Рік має бути від 1950 до 2100")]
        [Display(Name = "Рік до")]
        public int? YearTo { get; set; }

        [Range(0.0, 10000000.0, ErrorMessage = "Ціна має бути від 0 до 10 000 000")]
        [Display(Name = "Ціна від, $")]
        public decimal? PriceFrom { get; set; }

        [Range(0.0, 10000000.0, ErrorMessage = "Ціна має бути від 0 до 10 000 000")]
        [Display(Name = "Ціна до, $")]
        public decimal? PriceTo { get; set; }
    }

    public class CarsPage : CarFilterInput
    {
        public IList<Lab3Car> Cars { get; set; }
    }

    // ---------- Заявки клієнта ----------

    public class ClientOrdersInput
    {
        [Required(ErrorMessage = "Вкажіть e-mail клієнта")]
        [EmailAddress(ErrorMessage = "Некоректний e-mail")]
        [StringLength(256, ErrorMessage = "E-mail задовгий")]
        [Display(Name = "E-mail клієнта")]
        public string Email { get; set; }

        [Display(Name = "Статус заявки")]
        public string Status { get; set; }
    }

    public class ClientOrdersPage : ClientOrdersInput
    {
        public IList<ClientOrderRow> Rows { get; set; }
        public IList<string> ClientEmails { get; set; }
    }

    public class ClientOrderRow
    {
        public int OrderId { get; set; }
        public string ClientName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public decimal MaxBudget { get; set; }
        public decimal? FinalPrice { get; set; }
        public string ManagerName { get; set; }
    }

    // ---------- Сума оплачених інвойсів за період ----------

    public enum PeriodType
    {
        [Display(Name = "День")] Day,
        [Display(Name = "Місяць")] Month,
        [Display(Name = "Півріччя")] HalfYear
    }

    public class PaidInvoicesInput
    {
        [Required(ErrorMessage = "Оберіть тип періоду")]
        [Display(Name = "Період")]
        public PeriodType? Period { get; set; }

        [Required(ErrorMessage = "Вкажіть дату")]
        [DataType(DataType.Date)]
        [Display(Name = "Дата")]
        public DateTime? Date { get; set; }

        // Межі періоду [start; end): день, календарний місяць або півріччя (січень–червень / липень–грудень)
        public static void GetRange(PeriodType period, DateTime date, out DateTime start, out DateTime end)
        {
            switch (period)
            {
                case PeriodType.Day:
                    start = date.Date;
                    end = start.AddDays(1);
                    break;
                case PeriodType.Month:
                    start = new DateTime(date.Year, date.Month, 1);
                    end = start.AddMonths(1);
                    break;
                default:
                    start = new DateTime(date.Year, date.Month <= 6 ? 1 : 7, 1);
                    end = start.AddMonths(6);
                    break;
            }
        }
    }

    public class PaidInvoicesPage : PaidInvoicesInput
    {
        public bool Ran { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public IList<PaidInvoiceRow> Rows { get; set; }
    }

    public class PaidInvoiceSummary
    {
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    public class PaidInvoiceRow
    {
        public int InvoiceId { get; set; }
        public int OrderId { get; set; }
        public string ClientName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    // ---------- Доставки ----------

    public class DeliveriesInput
    {
        [Display(Name = "Статус доставки")]
        public string Status { get; set; }

        [Display(Name = "Митниця")]
        public bool? CustomsCleared { get; set; }
    }

    public class DeliveriesPage : DeliveriesInput
    {
        public IList<string> Statuses { get; set; }
        public IList<DeliveryRow> Rows { get; set; }
    }

    public class DeliveryRow
    {
        public int OrderId { get; set; }
        public string TrackingNumber { get; set; }
        public string Status { get; set; }
        public string CurrentLocation { get; set; }
        public bool IsCustomsCleared { get; set; }
        public string OrderStatus { get; set; }
        public string ClientName { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string VinCode { get; set; }
        public string LogisticianName { get; set; }
    }

    // ---------- Групування ----------

    public class StatsInput
    {
        // "status" — за статусом заявки, "make" — за маркою авто
        [Display(Name = "Групувати за")]
        public string GroupBy { get; set; }
    }

    public class StatsPage : StatsInput
    {
        public IList<GroupRow> Rows { get; set; }
    }

    public class GroupRow
    {
        public string Key { get; set; }
        public int OrdersCount { get; set; }
        public decimal AvgCarPrice { get; set; }
        public decimal MinCarPrice { get; set; }
        public decimal MaxCarPrice { get; set; }
    }

    // ---------- Керування страховками ----------

    public class InsuranceInput
    {
        public int? InsuranceId { get; set; }

        [Required(ErrorMessage = "Оберіть заявку")]
        [Display(Name = "Заявка")]
        public int? OrderId { get; set; }

        [Required(ErrorMessage = "Вкажіть тип страховки")]
        [StringLength(100, ErrorMessage = "Тип страховки задовгий (до 100 символів)")]
        [Display(Name = "Тип страховки")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Вкажіть ціну")]
        [Range(1.0, 1000000.0, ErrorMessage = "Ціна має бути від 1 до 1 000 000")]
        [Display(Name = "Ціна, $")]
        public decimal? Price { get; set; }
    }

    public class InsurancesPage : InsuranceInput
    {
        public IList<InsuranceRow> Rows { get; set; }
    }

    public class InsuranceRow
    {
        public int InsuranceId { get; set; }
        public int OrderId { get; set; }
        public string OrderStatus { get; set; }
        public string ClientName { get; set; }
        public string Type { get; set; }
        public decimal Price { get; set; }
    }

    // Українські назви статусів і ролей, що зберігаються в базі як імена перелічень OrderStatus / CarStatus / UserRole
    public static class Lab3Text
    {
        public static string OrderStatusName(string status)
        {
            OrderStatus value;
            return Enum.TryParse(status, out value) ? value.GetDisplayName() : status;
        }

        public static string CarStatusName(string status)
        {
            CarStatus value;
            return Enum.TryParse(status, out value) ? value.GetDisplayName() : status;
        }

        public static string RoleName(string role)
        {
            UserRole value;
            return Enum.TryParse(role, out value) ? value.GetDisplayName() : role;
        }
    }
}
