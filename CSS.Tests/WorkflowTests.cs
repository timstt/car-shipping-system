using System.Linq;
using ASTCUSA.Controllers;
using ASTCUSA.Models;
using ASTCUSA.Tests.Infrastructure;
using Xunit;

namespace ASTCUSA.Tests
{
    // ЛР5, повнота функціоналу: увесь шлях заявки з README від створення до відгуку,
    // кожен крок — дія контролера від імені відповідної ролі
    public class WorkflowTests
    {
        private readonly Seed seed = TestDb.Reset();

        private OrdersController As(int userId, UserRole role)
        {
            return Web.Controller<OrdersController>(userId, role);
        }

        private static Order Load(int orderId)
        {
            using (var db = new ImportContext())
                return db.Orders.Include("Car").Include("Invoices").Include("Delivery").Single(o => o.OrderId == orderId);
        }

        [Fact]
        public void Full_order_lifecycle_from_creation_to_review()
        {
            int client = seed.Client1Id, manager = seed.ManagerId, logist = seed.LogisticianId;

            using (var c = As(client, UserRole.Client)) c.Create(new Order { MaxBudget = 15000, CarId = seed.CamryId });
            int id;
            using (var db = new ImportContext()) id = db.Orders.Single().OrderId;

            using (var c = As(client, UserRole.Client)) c.AddInsurance(id, new Insurance { Type = "Базове", Price = 300 });

            using (var c = As(manager, UserRole.Manager)) c.ProcessOrder(id);
            Assert.Equal(OrderStatus.InProgress, Load(id).Status);

            using (var c = As(manager, UserRole.Manager)) c.RequestDocuments(id);
            Assert.Equal(OrderStatus.DocumentsRequested, Load(id).Status);
            using (var c = As(client, UserRole.Client)) c.ProvideDocuments(id);
            Assert.Equal(OrderStatus.DocumentsProvided, Load(id).Status);
            using (var c = As(manager, UserRole.Manager)) c.ProcessOrder(id);

            using (var c = As(manager, UserRole.Manager)) c.PlaceBid(id, 12500);
            var order = Load(id);
            Assert.Equal(OrderStatus.BidWon, order.Status);
            Assert.Equal(CarStatus.Reserved, order.Car.Status);

            using (var c = As(manager, UserRole.Manager)) c.CreateInvoice(id);
            order = Load(id);
            Assert.Equal(OrderStatus.Invoiced, order.Status);
            Assert.Equal(12500, order.Invoice.TotalAmount);

            using (var c = As(client, UserRole.Client)) c.PayInvoice(id);
            Assert.Equal(OrderStatus.PaymentPending, Load(id).Status);

            using (var c = As(manager, UserRole.Manager)) c.ConfirmPayment(id);
            order = Load(id);
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.Equal(CarStatus.Paid, order.Car.Status);

            using (var c = As(logist, UserRole.Logistician)) c.CreateDelivery(id);
            Assert.NotNull(Load(id).Delivery);
            using (var c = As(logist, UserRole.Logistician)) c.AddCargoInsurance(id, 250);
            using (var c = As(logist, UserRole.Logistician)) c.AddPhotoReport(id, "Без пошкоджень");
            using (var c = As(logist, UserRole.Logistician)) c.ClearCustoms(id);
            order = Load(id);
            Assert.Equal(OrderStatus.Shipping, order.Status);
            Assert.Equal(CarStatus.InTransit, order.Car.Status);

            using (var c = As(logist, UserRole.Logistician)) c.UpdateDeliveryStatus(id, "Одеса, порт", true);
            order = Load(id);
            Assert.Equal(CarStatus.ReadyForPickup, order.Car.Status);
            Assert.Equal("Одеса, порт", order.Delivery.CurrentLocation);

            using (var c = As(client, UserRole.Client)) c.ConfirmReceipt(id);
            using (var c = As(client, UserRole.Client)) c.LeaveReview(id, "Усе чудово");
            order = Load(id);
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.Equal(CarStatus.Delivered, order.Car.Status);
            Assert.Equal("Усе чудово", order.Review);
            Assert.True(order.Delivery.HasCargoInsurance);
        }

        // Бюджет менший за ціну авто → «Ставку програно», клієнта сповіщено
        [Fact]
        public void Bid_is_lost_when_budget_is_too_small()
        {
            int id = TestDb.AddOrder(seed.Client1Id, seed.CivicId, OrderStatus.InProgress, budget: 10000);
            using (var c = As(seed.ManagerId, UserRole.Manager)) c.PlaceBid(id, 9000);
            Assert.Equal(OrderStatus.BidLost, Load(id).Status);
            using (var db = new ImportContext())
                Assert.Contains("бюджет", db.Clients.Find(seed.Client1Id).Notification);
        }
    }
}
