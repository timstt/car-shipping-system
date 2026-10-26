using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using System.Web.Security;

namespace ASTCUSA.Models
{
    // «abstract» User з діаграми класів. Спадкування зберігається в одній таблиці (TPH).
    public abstract class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Вкажіть ім'я")]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }

        // Логін користувача: унікальний, зберігається в нижньому регістрі
        [Required(ErrorMessage = "Вкажіть e-mail"), EmailAddress(ErrorMessage = "Некоректний e-mail")]
        [StringLength(256)]
        [Index("IX_User_Email", IsUnique = true)]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        // PBKDF2-хеш пароля разом із сіллю (див. PasswordHasher). Сам пароль не зберігається.
        [Required]
        [StringLength(256)]
        public string PasswordHash { get; set; }

        [NotMapped]
        public abstract UserRole Role { get; }

        public static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        public void SetPassword(string password)
        {
            PasswordHash = PasswordHasher.Hash(password);
        }

        public bool CheckPassword(string password)
        {
            return PasswordHasher.Verify(password, PasswordHash);
        }

        // login(): bool — перевіряє пароль і видає авторизаційний cookie FormsAuthentication
        // (Id і роль записуються в тікет)
        public bool Login(string password, HttpContextBase context)
        {
            if (context == null || !CheckPassword(password)) return false;
            context.Session?.Clear(); // дані попереднього користувача (обране) не переходять до нового
            context.Response.Cookies.Add(AuthTicket.CreateCookie(this));
            return true;
        }

        // logout(): void — видаляє авторизаційний cookie і дані сесії
        public void Logout(HttpContextBase context)
        {
            SignOut(context);
        }

        // Вихід, коли користувача вже немає в БД (наприклад, базу перестворено)
        public static void SignOut(HttpContextBase context)
        {
            FormsAuthentication.SignOut();
            if (context != null) context.Session?.Clear();
        }
    }

    public class Client : User
    {
        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Адреса")]
        public string Address { get; set; }

        // Останнє сповіщення від менеджера (notifyClient)
        [Display(Name = "Сповіщення")]
        public string Notification { get; set; }

        public virtual ICollection<Order> Orders { get; set; }

        public override UserRole Role { get { return UserRole.Client; } }

        // createOrder(budget: double): Order
        public Order CreateOrder(double budget)
        {
            var order = new Order { ClientId = Id, CreatedAt = DateTime.Now, Status = OrderStatus.New };
            order.ChangeBudget(budget);
            return order;
        }

        // payInvoice(payable: IPayable): bool
        public bool PayInvoice(IPayable payable)
        {
            if (payable == null || payable.CheckPaymentStatus()) return false;
            payable.MarkAsPaid();
            return true;
        }

        // provideDocuments(order: Order): void
        public void ProvideDocuments(Order order)
        {
            if (order.Status != OrderStatus.DocumentsRequested)
                throw new InvalidOperationException("Документи не запитувалися.");
            order.UpdateStatus(OrderStatus.DocumentsProvided);
        }

        // confirmReceipt(order: Order): void — авто стає «Доставлено», заявка «Завершено»
        public void ConfirmReceipt(Order order)
        {
            if (order.Car == null || order.Car.Status != CarStatus.ReadyForPickup)
                throw new InvalidOperationException("Авто ще не готове до видачі.");
            order.IsConfirmedByClient = true;
            order.Car.Status = CarStatus.Delivered;
            order.UpdateStatus(OrderStatus.Completed);
        }

        // trackDelivery(trackingNumber: String): string
        public string TrackDelivery(string trackingNumber)
        {
            var delivery = (Orders ?? new List<Order>())
                .Select(o => o.Delivery)
                .FirstOrDefault(d => d != null && d.TrackingNumber == trackingNumber);
            if (delivery == null) return "Доставку з таким номером не знайдено.";
            return string.Format("{0}: {1}. Митниця: {2}.",
                delivery.CurrentLocation, delivery.Status,
                delivery.IsCustomsCleared ? "пройдено" : "не пройдено");
        }
    }

    public class Manager : User
    {
        public virtual ICollection<Order> Orders { get; set; }

        public override UserRole Role { get { return UserRole.Manager; } }

        // takeOrderInWork(order: Order): void
        public void TakeOrderInWork(Order order)
        {
            if (order.Status != OrderStatus.New && order.Status != OrderStatus.DocumentsProvided)
                throw new InvalidOperationException("Заявку вже оброблено.");
            order.ManagerId = Id;
            order.UpdateStatus(OrderStatus.InProgress);
        }

        // requestDocuments(order: Order): void
        public void RequestDocuments(Order order)
        {
            if (order.Status != OrderStatus.InProgress)
                throw new InvalidOperationException("Документи можна запитати лише для заявки в роботі.");
            order.UpdateStatus(OrderStatus.DocumentsRequested);
        }

        // placeBid(car: Car, amount: double): bool — Доступно → Аукціон → Зарезервовано / Продано іншому
        public bool PlaceBid(Car car, double amount)
        {
            if (car.Status != CarStatus.Available)
                throw new InvalidOperationException("Авто недоступне для аукціону.");
            car.Status = CarStatus.Auction;
            bool won = amount >= car.AuctionPrice;
            car.Status = won ? CarStatus.Reserved : CarStatus.SoldToOther;
            return won;
        }

        // generateInvoice(order: Order): Invoice — сума дорівнює виграній ставці
        public Invoice GenerateInvoice(Order order)
        {
            if (order.Status != OrderStatus.BidWon || order.FinalPrice == null)
                throw new InvalidOperationException("Інвойс можна виставити лише після виграної ставки.");
            order.UpdateStatus(OrderStatus.Invoiced);
            return new Invoice { OrderId = order.OrderId, TotalAmount = order.FinalPrice.Value, IsPaid = false };
        }

        // confirmPayment(invoice: Invoice): void — заявка й авто стають «Оплачено»
        public void ConfirmPayment(Invoice invoice)
        {
            if (!invoice.CheckPaymentStatus())
                throw new InvalidOperationException("Оплата ще не надійшла.");
            invoice.Order.UpdateStatus(OrderStatus.Paid);
            if (invoice.Order.Car != null) invoice.Order.Car.Status = CarStatus.Paid;
        }

        // notifyClient(client: Client): void
        public void NotifyClient(Client client, string message)
        {
            client.Notification = string.Format("{0:dd.MM.yyyy HH:mm} — {1}", DateTime.Now, message);
        }
    }

    public class Logistician : User
    {
        public virtual ICollection<Delivery> Deliveries { get; set; }

        public override UserRole Role { get { return UserRole.Logistician; } }

        // assignCarrier(delivery: Delivery): void
        public void AssignCarrier(Delivery delivery)
        {
            delivery.LogisticianId = Id;
            delivery.TrackingNumber = "TRK-" + delivery.OrderId + "-" + DateTime.Now.ToString("yyMMddHHmm");
            delivery.CurrentLocation = "США, склад аукціону";
            delivery.Status = "Перевізника призначено";
        }

        // createCustomsDeclaration(delivery: Delivery): void — після митниці авто «В дорозі»
        public void CreateCustomsDeclaration(Delivery delivery)
        {
            delivery.ClearCustoms();
            delivery.Order.UpdateStatus(OrderStatus.Shipping);
            if (delivery.Order.Car != null) delivery.Order.Car.Status = CarStatus.InTransit;
        }

        // generateMandate(delivery: Delivery): void
        public void GenerateMandate(Delivery delivery)
        {
            delivery.MandateGeneratedAt = DateTime.Now;
        }

        // updateDeliveryStatus(delivery: Delivery, status: String): void
        public void UpdateDeliveryStatus(Delivery delivery, string status)
        {
            if (!delivery.IsCustomsCleared)
                throw new InvalidOperationException("Спочатку оформіть митні документи.");
            delivery.Status = status;
        }
    }
}
