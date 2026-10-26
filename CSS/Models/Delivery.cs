using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASTCUSA.Models
{
    public class Delivery
    {
        // Order 1 — 0..1 Delivery: ключ доставки збігається з ключем заявки
        [Key, ForeignKey("Order")]
        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Display(Name = "Трек-номер")]
        public string TrackingNumber { get; set; }

        [Display(Name = "Поточне місце")]
        public string CurrentLocation { get; set; }

        [Display(Name = "Митницю пройдено")]
        public bool IsCustomsCleared { get; set; }

        [Display(Name = "Статус доставки")]
        public string Status { get; set; }

        [Display(Name = "Фотозвіт стану авто")]
        public string PhotoReportNote { get; set; }

        [Display(Name = "Додаткове страхування вантажу")]
        public bool HasCargoInsurance { get; set; }

        [Display(Name = "Вартість страхування вантажу, $")]
        public double CargoInsuranceCost { get; set; }

        [Display(Name = "Доручення сформовано")]
        public DateTime? MandateGeneratedAt { get; set; }

        // Logistician 0..1 — 0..* Delivery («організовує»)
        public int? LogisticianId { get; set; }
        public virtual Logistician Logistician { get; set; }

        // updateLocation(location: String): void
        public void UpdateLocation(string location)
        {
            if (!string.IsNullOrWhiteSpace(location)) CurrentLocation = location.Trim();
        }

        // clearCustoms(): void
        public void ClearCustoms()
        {
            if (IsCustomsCleared) throw new InvalidOperationException("Митницю вже пройдено.");
            IsCustomsCleared = true;
            Status = "Митницю пройдено, авто в дорозі";
        }
    }
}
