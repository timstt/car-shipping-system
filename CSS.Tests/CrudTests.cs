using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Controllers;
using ASTCUSA.Models;
using ASTCUSA.Tests.Infrastructure;
using Xunit;

namespace ASTCUSA.Tests
{
    // ЛР5: Create / Read / Update / Delete для сутностей основної бази через ті самі дії
    // контролерів, які викликає сайт (кожен виклик — окремий DbContext, як окремий HTTP-запит)
    public class CrudTests
    {
        private readonly Seed seed = TestDb.Reset();

        // ---------- Car (менеджер) ----------

        [Fact]
        public void Car_create_read_update_delete()
        {
            using (var c = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
                Assert.IsType<RedirectToRouteResult>(c.Create(new Car
                {
                    VinCode = "WBA8E9C51GK644321", Make = "BMW", Model = "328i", Year = 2016, AuctionPrice = 9900
                }));

            Car created;
            using (var db = new ImportContext())
                created = db.Cars.AsNoTracking().Single(x => x.VinCode == "WBA8E9C51GK644321");
            Assert.Equal(CarStatus.Available, created.Status);

            using (var c = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
            {
                var view = Assert.IsType<ViewResult>(c.Details(created.CarId));
                Assert.Equal("BMW", ((Car)view.Model).Make);
            }

            created.Model = "330i";
            created.AuctionPrice = 10500;
            using (var c = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
                Assert.IsType<RedirectToRouteResult>(c.Edit(created));
            using (var db = new ImportContext())
            {
                var updated = db.Cars.Find(created.CarId);
                Assert.Equal("330i", updated.Model);
                Assert.Equal(10500, updated.AuctionPrice);
            }

            using (var c = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
                c.DeleteConfirmed(created.CarId);
            using (var db = new ImportContext())
                Assert.Null(db.Cars.Find(created.CarId));
        }

        // Авто, до якого прив'язана заявка, не видаляється (зовнішній ключ Orders.CarId)
        [Fact]
        public void Car_with_orders_is_not_deleted()
        {
            TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.New);
            using (var c = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
                c.DeleteConfirmed(seed.CamryId);
            using (var db = new ImportContext())
                Assert.NotNull(db.Cars.Find(seed.CamryId));
        }

        // ---------- Order (клієнт створює й змінює, менеджер видаляє) ----------

        [Fact]
        public void Order_create_read_update_delete()
        {
            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                Assert.IsType<RedirectToRouteResult>(c.Create(new Order { MaxBudget = 15000, CarId = seed.CamryId }));

            Order order;
            using (var db = new ImportContext())
                order = db.Orders.AsNoTracking().Single(o => o.ClientId == seed.Client1Id);
            Assert.Equal(OrderStatus.New, order.Status);
            Assert.Equal(seed.CamryId, order.CarId);

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
            {
                var view = Assert.IsType<ViewResult>(c.Index(null));
                Assert.Single((System.Collections.Generic.IEnumerable<Order>)view.Model);
            }

            order.MaxBudget = 20000;
            order.CarId = seed.CivicId;
            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                c.Edit(order);
            using (var db = new ImportContext())
            {
                var updated = db.Orders.Find(order.OrderId);
                Assert.Equal(20000, updated.MaxBudget);
                Assert.Equal(seed.CivicId, updated.CarId);
            }

            using (var c = Web.Controller<OrdersController>(seed.ManagerId, UserRole.Manager))
                c.DeleteConfirmed(order.OrderId);
            using (var db = new ImportContext())
                Assert.Null(db.Orders.Find(order.OrderId));
        }

        // Видалення заявки прибирає і залежні страховки (одним SaveChanges)
        [Fact]
        public void Order_delete_removes_dependent_insurances()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.Cancelled);
            using (var db = new ImportContext())
            {
                db.Insurances.Add(new Insurance { OrderId = orderId, Type = "Базове", Price = 300 });
                db.SaveChanges();
            }
            using (var c = Web.Controller<OrdersController>(seed.ManagerId, UserRole.Manager))
                c.DeleteConfirmed(orderId);
            using (var db = new ImportContext())
            {
                Assert.Null(db.Orders.Find(orderId));
                Assert.False(db.Insurances.Any(i => i.OrderId == orderId));
            }
        }

        // Заявку в роботі (не нову/скасовану/програну) видалити не можна
        [Fact]
        public void Order_in_progress_is_not_deleted()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.InProgress);
            using (var c = Web.Controller<OrdersController>(seed.ManagerId, UserRole.Manager))
                c.DeleteConfirmed(orderId);
            using (var db = new ImportContext())
                Assert.NotNull(db.Orders.Find(orderId));
        }

        // ---------- Insurance (клієнт, до оплати) ----------

        [Fact]
        public void Insurance_create_update_delete_before_payment()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.New);

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                c.AddInsurance(orderId, new Insurance { Type = "Базове", Price = 300 });
            int insuranceId;
            using (var db = new ImportContext())
                insuranceId = db.Insurances.Single(i => i.OrderId == orderId).InsuranceId;

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                c.EditInsurance(orderId, insuranceId, new Insurance { Type = "Повне", Price = 900 });
            using (var db = new ImportContext())
            {
                var ins = db.Insurances.Find(insuranceId);
                Assert.Equal("Повне", ins.Type);
                Assert.Equal(900, ins.Price);
            }

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
                c.DeleteInsurance(orderId, insuranceId);
            using (var db = new ImportContext())
                Assert.Null(db.Insurances.Find(insuranceId));
        }

        // Страховки оплаченої заявки заблоковані
        [Fact]
        public void Insurance_of_paid_order_cannot_be_changed_or_deleted()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.Paid);
            int insuranceId;
            using (var db = new ImportContext())
            {
                var ins = new Insurance { OrderId = orderId, Type = "Базове", Price = 300 };
                db.Insurances.Add(ins);
                db.SaveChanges();
                insuranceId = ins.InsuranceId;
            }

            using (var c = Web.Controller<OrdersController>(seed.Client1Id, UserRole.Client))
            {
                c.EditInsurance(orderId, insuranceId, new Insurance { Type = "Повне", Price = 900 });
                c.DeleteInsurance(orderId, insuranceId);
            }
            using (var db = new ImportContext())
            {
                var ins = db.Insurances.Find(insuranceId);
                Assert.NotNull(ins);
                Assert.Equal("Базове", ins.Type);
            }
        }

        // ---------- Client (профіль) ----------

        [Fact]
        public void Client_profile_read_update_delete()
        {
            using (var c = Web.Controller<ProfileController>(seed.Client2Id, UserRole.Client))
            {
                var view = Assert.IsType<ViewResult>(c.Index());
                Assert.Equal("client2@test.com", ((ProfileViewModel)view.Model).Email);
            }

            using (var c = Web.Controller<ProfileController>(seed.Client2Id, UserRole.Client))
                c.Edit(new ProfileViewModel { Name = "  Нове Ім'я ", PhoneNumber = "+380671111111", Address = "Львів" });
            using (var db = new ImportContext())
            {
                var client = db.Clients.Find(seed.Client2Id);
                Assert.Equal("Нове Ім'я", client.Name);
                Assert.Equal("+380671111111", client.PhoneNumber);
                Assert.Equal("Львів", client.Address);
                Assert.Equal("client2@test.com", client.Email); // e-mail не змінюється
            }

            // Після видалення контролер викликає FormsAuthentication.SignOut(), а він поза IIS
            // кидає NullReferenceException. Тут перевіряємо саме видалення з БД, яке відбувається до виходу.
            using (var c = Web.Controller<ProfileController>(seed.Client2Id, UserRole.Client))
            {
                var ex = Record.Exception(() => c.DeleteConfirmed());
                Assert.True(ex == null || ex is NullReferenceException);
            }
            using (var db = new ImportContext())
                Assert.Null(db.Users.Find(seed.Client2Id));
        }

        // Клієнта із заявками не видаляємо: каскад інакше стер би історію угод
        [Fact]
        public void Client_with_orders_is_not_deleted()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.New);
            using (var c = Web.Controller<ProfileController>(seed.Client1Id, UserRole.Client))
                c.DeleteConfirmed();
            using (var db = new ImportContext())
            {
                Assert.NotNull(db.Users.Find(seed.Client1Id));
                Assert.NotNull(db.Orders.Find(orderId));
            }
        }

        // ---------- Пастка з методички: об'єкт з іншого контексту ----------

        // Зміни об'єкта, прочитаного в іншому контексті, не зберігаються, поки новий контекст
        // не дізнається про них: Entry(obj).State = Modified (оновлення) або Deleted (видалення)
        [Fact]
        public void Detached_entity_needs_explicit_state()
        {
            Car car;
            using (var db = new ImportContext())
                car = db.Cars.Find(seed.CamryId);

            using (var db = new ImportContext())
            {
                car.AuctionPrice = 13000;
                db.SaveChanges(); // контекст не відстежує car — нічого не збережено
            }
            using (var db = new ImportContext())
                Assert.Equal(12000, db.Cars.Find(seed.CamryId).AuctionPrice);

            using (var db = new ImportContext())
            {
                db.Entry(car).State = EntityState.Modified;
                db.SaveChanges();
            }
            using (var db = new ImportContext())
                Assert.Equal(13000, db.Cars.Find(seed.CamryId).AuctionPrice);

            Car civic;
            using (var db = new ImportContext())
                civic = db.Cars.Find(seed.CivicId);
            using (var db = new ImportContext())
            {
                db.Cars.Attach(civic);   // приєднати до контексту
                db.Cars.Remove(civic);   // → EntityState.Deleted
                db.SaveChanges();
            }
            using (var db = new ImportContext())
                Assert.Null(db.Cars.Find(seed.CivicId));
        }
    }
}
