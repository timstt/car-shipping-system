using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace ASTCUSA.Models
{
    public class Order
    {
        [Display(Name = "№ заявки")]
        public int OrderId { get; set; }

        [Range(1, 10000000, ErrorMessage = "Бюджет має бути більшим за 0")]
        [Display(Name = "Максимальний бюджет, $")]
        public double MaxBudget { get; set; }

        [Display(Name = "Фінальна ціна, $")]
        public double? FinalPrice { get; set; }

        [Display(Name = "Статус")]
        public OrderStatus Status { get; set; }

        [Display(Name = "Створено")]
        public DateTime CreatedAt { get; set; }

        // Client 1 — 0..* Order («створює»)
        [Display(Name = "Клієнт")]
        public int ClientId { get; set; }
        [InverseProperty("Orders")]
        public virtual Client Client { get; set; }

        // Manager 0..1 — 0..* Order («обробляє»)
        [Display(Name = "Менеджер")]
        public int? ManagerId { get; set; }
        [InverseProperty("Orders")]
        public virtual Manager Manager { get; set; }

        // Order — Car 0..1 («стосується»)
        [Display(Name = "Авто")]
        public int? CarId { get; set; }
        public virtual Car Car { get; set; }

        // Order 1 — 0..* Insurance («має страховку»)
        public virtual ICollection<Insurance> Insurances { get; set; }

        // Order 1 ◆— 0..1 Invoice («містить»). У БД це 1:N з унікальним індексом на Invoice.OrderId,
        // тому колекція Invoices містить не більше одного інвойсу.
        public virtual ICollection<Invoice> Invoices { get; set; }

        [NotMapped]
        public Invoice Invoice { get { return Invoices == null ? null : Invoices.FirstOrDefault(); } }

        // Order 1 — 0..1 Delivery («включає»)
        public virtual Delivery Delivery { get; set; }

        [Display(Name = "Отримання підтверджено")]
        public bool IsConfirmedByClient { get; set; }

        [Display(Name = "Відгук")]
        public string Review { get; set; }

        // Заявка «активна», поки авто в роботі; завершені/скасовані/програні не блокують авто.
        public static readonly OrderStatus[] InactiveStatuses =
            { OrderStatus.Cancelled, OrderStatus.BidLost, OrderStatus.Completed };

        // updateStatus(newStatus): void
        public void UpdateStatus(OrderStatus newStatus)
        {
            Status = newStatus;
        }

        // changeBudget(newBudget: double): void
        public void ChangeBudget(double newBudget)
        {
            if (newBudget <= 0) throw new ArgumentException("Бюджет має бути більшим за 0.");
            MaxBudget = newBudget;
        }

        // assignCar(car: Car): void
        public void AssignCar(Car car)
        {
            if (car != null && car.Status != CarStatus.Available)
                throw new InvalidOperationException("Це авто вже недоступне.");
            Car = car;
            CarId = car == null ? (int?)null : car.CarId;
        }

        // addInsurance(insurance: Insurance): void
        public void AddInsurance(Insurance insurance)
        {
            if (Status == OrderStatus.Paid || Status == OrderStatus.Shipping ||
                Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
                throw new InvalidOperationException("Страховку можна додати лише до оплати заявки.");
            if (Insurances == null) Insurances = new List<Insurance>();
            insurance.OrderId = OrderId;
            Insurances.Add(insurance);
        }
    }
}
