using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASTCUSA.Models.Analytics
{
    // Прості класи-відображення таблиць бази astcusa_analytics.
    // Окремі від основних моделей, щоб не впливати на Code First-базу ImportContext.
    // Префікс Analytics потрібен, щоб імена не збігалися з класами ASTCUSA.Models.

    // «abstract» User + Client / Manager / Logistician: одна таблиця, роль у стовпці Role
    [Table("Users")]
    public class AnalyticsUser
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
    }

    [Table("Cars")]
    public class AnalyticsCar
    {
        [Key]
        public int CarId { get; set; }
        public string VinCode { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public decimal AuctionPrice { get; set; }
        public string Status { get; set; }
    }

    [Table("Orders")]
    public class AnalyticsOrder
    {
        [Key]
        public int OrderId { get; set; }
        public int ClientId { get; set; }
        public int? ManagerId { get; set; }
        public int? CarId { get; set; }
        public decimal MaxBudget { get; set; }
        public decimal? FinalPrice { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("Insurances")]
    public class AnalyticsInsurance
    {
        [Key]
        public int InsuranceId { get; set; }
        public int OrderId { get; set; }
        public string Type { get; set; }
        public decimal Price { get; set; }
    }

    [Table("Invoices")]
    public class AnalyticsInvoice
    {
        [Key]
        public int InvoiceId { get; set; }
        public int OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    // Ключ доставки збігається з ключем заявки (Order 1 — 0..1 Delivery)
    [Table("Deliveries")]
    public class AnalyticsDelivery
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int OrderId { get; set; }
        public int? LogisticianId { get; set; }
        public string TrackingNumber { get; set; }
        public string CurrentLocation { get; set; }
        public bool IsCustomsCleared { get; set; }
        public string Status { get; set; }
    }
}
