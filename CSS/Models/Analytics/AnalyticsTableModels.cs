using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;

namespace ASTCUSA.Models.Analytics
{
    // Форми додавання / зміни записів у таблицях бази astcusa_analytics (ЛР4).
    // Атрибути повторюють обмеження таблиць (create_and_seed.sql), щоб помилку
    // показати біля поля, а не отримати виняток від SQL Server.

    public class UserInput
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Вкажіть ім'я")]
        [StringLength(100, ErrorMessage = "Ім'я задовге (до 100 символів)")]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Вкажіть e-mail")]
        [EmailAddress(ErrorMessage = "Некоректний e-mail")]
        [StringLength(256, ErrorMessage = "E-mail задовгий")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Оберіть роль")]
        [Display(Name = "Роль")]
        public string Role { get; set; }

        [StringLength(30, ErrorMessage = "Телефон задовгий (до 30 символів)")]
        [Display(Name = "Телефон (лише для клієнта)")]
        public string PhoneNumber { get; set; }

        [StringLength(200, ErrorMessage = "Адреса задовга (до 200 символів)")]
        [Display(Name = "Адреса (лише для клієнта)")]
        public string Address { get; set; }
    }

    public class CarInput
    {
        public int? CarId { get; set; }

        [Required(ErrorMessage = "Вкажіть VIN")]
        [StringLength(17, MinimumLength = 11, ErrorMessage = "VIN має містити від 11 до 17 символів")]
        [Display(Name = "VIN")]
        public string VinCode { get; set; }

        [Required(ErrorMessage = "Вкажіть марку")]
        [StringLength(50, ErrorMessage = "Марка задовга (до 50 символів)")]
        [Display(Name = "Марка")]
        public string Make { get; set; }

        [Required(ErrorMessage = "Вкажіть модель")]
        [StringLength(50, ErrorMessage = "Модель задовга (до 50 символів)")]
        [Display(Name = "Модель")]
        public string Model { get; set; }

        [Required(ErrorMessage = "Вкажіть рік")]
        [Range(1950, 2100, ErrorMessage = "Рік має бути від 1950 до 2100")]
        [Display(Name = "Рік")]
        public int? Year { get; set; }

        [Required(ErrorMessage = "Вкажіть ціну")]
        [Range(1.0, 10000000.0, ErrorMessage = "Ціна має бути від 1 до 10 000 000")]
        [Display(Name = "Ціна на аукціоні, $")]
        public decimal? AuctionPrice { get; set; }

        [Required(ErrorMessage = "Оберіть статус")]
        [Display(Name = "Статус")]
        public string Status { get; set; }
    }

    public class OrderInput
    {
        public int? OrderId { get; set; }

        [Required(ErrorMessage = "Оберіть клієнта")]
        [Display(Name = "Клієнт")]
        public int? ClientId { get; set; }

        [Display(Name = "Менеджер")]
        public int? ManagerId { get; set; }

        [Display(Name = "Авто")]
        public int? CarId { get; set; }

        [Required(ErrorMessage = "Вкажіть бюджет")]
        [Range(1.0, 10000000.0, ErrorMessage = "Бюджет має бути від 1 до 10 000 000")]
        [Display(Name = "Макс. бюджет, $")]
        public decimal? MaxBudget { get; set; }

        [Range(1.0, 10000000.0, ErrorMessage = "Підсумкова ціна має бути від 1 до 10 000 000")]
        [Display(Name = "Підсумкова ціна, $")]
        public decimal? FinalPrice { get; set; }

        [Required(ErrorMessage = "Оберіть статус")]
        [Display(Name = "Статус")]
        public string Status { get; set; }

        [Required(ErrorMessage = "Вкажіть дату створення")]
        [DataType(DataType.Date)]
        [Display(Name = "Дата створення")]
        public DateTime? CreatedAt { get; set; }
    }

    public class InvoiceInput
    {
        public int? InvoiceId { get; set; }

        [Required(ErrorMessage = "Оберіть заявку")]
        [Display(Name = "Заявка")]
        public int? OrderId { get; set; }

        [Required(ErrorMessage = "Вкажіть суму")]
        [Range(1.0, 10000000.0, ErrorMessage = "Сума має бути від 1 до 10 000 000")]
        [Display(Name = "Сума, $")]
        public decimal? TotalAmount { get; set; }

        [Display(Name = "Оплачено")]
        public bool IsPaid { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Дата оплати")]
        public DateTime? PaidAt { get; set; }
    }

    public class DeliveryInput
    {
        // Ключ доставки = номер заявки; при зміні запису його не можна змінити
        [Required(ErrorMessage = "Оберіть заявку")]
        [Display(Name = "Заявка")]
        public int? OrderId { get; set; }

        [Display(Name = "Логіст")]
        public int? LogisticianId { get; set; }

        [Required(ErrorMessage = "Вкажіть номер відстеження")]
        [StringLength(30, ErrorMessage = "Номер відстеження задовгий (до 30 символів)")]
        [Display(Name = "Номер відстеження")]
        public string TrackingNumber { get; set; }

        [StringLength(100, ErrorMessage = "Місцезнаходження задовге (до 100 символів)")]
        [Display(Name = "Поточне місцезнаходження")]
        public string CurrentLocation { get; set; }

        [Display(Name = "Митницю пройдено")]
        public bool IsCustomsCleared { get; set; }

        [Required(ErrorMessage = "Вкажіть статус доставки")]
        [StringLength(50, ErrorMessage = "Статус задовгий (до 50 символів)")]
        [Display(Name = "Статус доставки")]
        public string Status { get; set; }
    }

    // ---------- Рядки таблиць для списків ----------

    public class OrderTableRow
    {
        public int OrderId { get; set; }
        public string ClientName { get; set; }
        public string ManagerName { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public decimal MaxBudget { get; set; }
        public decimal? FinalPrice { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InvoiceTableRow
    {
        public int InvoiceId { get; set; }
        public int OrderId { get; set; }
        public string ClientName { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class DeliveryTableRow
    {
        public int OrderId { get; set; }
        public string ClientName { get; set; }
        public string LogisticianName { get; set; }
        public string TrackingNumber { get; set; }
        public string CurrentLocation { get; set; }
        public bool IsCustomsCleared { get; set; }
        public string Status { get; set; }
    }

    // Списки вибору для форм: значення перелічень зберігаються в базі як їхні імена (Available, New, Client …)
    public static class AnalyticsChoices
    {
        public static SelectList Enum<T>(string selected, params T[] skip) where T : struct
        {
            var items = System.Enum.GetValues(typeof(T)).Cast<T>()
                .Where(v => !skip.Contains(v))
                .Select(v => new SelectListItem { Value = v.ToString(), Text = ((System.Enum)(object)v).GetDisplayName() });
            return new SelectList(items, "Value", "Text", selected);
        }

        public static bool IsEnumName<T>(string value) where T : struct
        {
            T parsed;
            return !string.IsNullOrEmpty(value) && System.Enum.TryParse(value, false, out parsed)
                && System.Enum.IsDefined(typeof(T), value);
        }

        public static SelectList Pairs(IEnumerable<KeyValuePair<int, string>> pairs, int? selected)
        {
            return new SelectList(pairs, "Key", "Value", selected);
        }
    }
}
