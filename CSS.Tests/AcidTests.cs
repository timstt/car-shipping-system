using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Controllers;
using ASTCUSA.Models;
using ASTCUSA.Tests.Infrastructure;
using Xunit;

namespace ASTCUSA.Tests
{
    // ЛР5: перевірка властивостей ACID на реальній SQL Server LocalDB (база astcusa_tests)
    public class AcidTests
    {
        private readonly Seed seed = TestDb.Reset();

        private static Car NewCar(string vin)
        {
            return new Car { VinCode = vin, Make = "Ford", Model = "Focus", Year = 2018, AuctionPrice = 8000, Status = CarStatus.Available };
        }

        // ---------- Atomicity ----------

        // Один SaveChanges = одна транзакція: якщо одна зміна порушує обмеження,
        // не зберігається жодна (ні нове авто, ні зміна статусу заявки, ні другий інвойс)
        [Fact]
        public void Atomicity_SaveChanges_saves_all_or_nothing()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.BidWon);
            using (var db = new ImportContext())
            {
                db.Invoices.Add(new Invoice { OrderId = orderId, TotalAmount = 12000 });
                db.SaveChanges();
            }

            using (var db = new ImportContext())
            {
                db.Cars.Add(NewCar("AAAAAAAAAAA000001"));                              // коректна зміна
                db.Orders.Find(orderId).UpdateStatus(OrderStatus.Invoiced);             // коректна зміна
                db.Invoices.Add(new Invoice { OrderId = orderId, TotalAmount = 1 });    // другий інвойс → IX_Invoice_OrderId
                Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            }

            using (var db = new ImportContext())
            {
                Assert.False(db.Cars.Any(c => c.VinCode == "AAAAAAAAAAA000001"));
                Assert.Equal(OrderStatus.BidWon, db.Orders.Find(orderId).Status);
                Assert.Equal(1, db.Invoices.Count(i => i.OrderId == orderId));
            }
        }

        // Явна транзакція з кількома SaveChanges: Rollback скасовує все, Commit — фіксує все
        [Fact]
        public void Atomicity_explicit_transaction_rollback_and_commit()
        {
            using (var db = new ImportContext())
            using (var tx = db.Database.BeginTransaction())
            {
                db.Cars.Add(NewCar("BBBBBBBBBBB000001"));
                db.SaveChanges();
                db.Cars.Find(seed.CamryId).UpdatePrice(99999);
                db.SaveChanges();
                tx.Rollback();
            }
            using (var db = new ImportContext())
            {
                Assert.False(db.Cars.Any(c => c.VinCode == "BBBBBBBBBBB000001"));
                Assert.Equal(12000, db.Cars.Find(seed.CamryId).AuctionPrice);
            }

            using (var db = new ImportContext())
            using (var tx = db.Database.BeginTransaction())
            {
                db.Cars.Add(NewCar("BBBBBBBBBBB000002"));
                db.SaveChanges();
                db.Cars.Find(seed.CamryId).UpdatePrice(13000);
                db.SaveChanges();
                tx.Commit();
            }
            using (var db = new ImportContext())
            {
                Assert.True(db.Cars.Any(c => c.VinCode == "BBBBBBBBBBB000002"));
                Assert.Equal(13000, db.Cars.Find(seed.CamryId).AuctionPrice);
            }
        }

        // Бізнес-операція «Зафіксувати оплату» змінює дві таблиці (заявка й авто) одним SaveChanges
        [Fact]
        public void Atomicity_confirm_payment_updates_order_and_car_together()
        {
            int orderId = TestDb.AddOrder(seed.Client1Id, seed.CamryId, OrderStatus.PaymentPending);
            using (var db = new ImportContext())
            {
                db.Invoices.Add(new Invoice { OrderId = orderId, TotalAmount = 12000, IsPaid = true });
                db.Cars.Find(seed.CamryId).Status = CarStatus.Reserved;
                db.SaveChanges();
            }

            using (var controller = Web.Controller<OrdersController>(seed.ManagerId, UserRole.Manager))
                controller.ConfirmPayment(orderId);

            using (var db = new ImportContext())
            {
                Assert.Equal(OrderStatus.Paid, db.Orders.Find(orderId).Status);
                Assert.Equal(CarStatus.Paid, db.Cars.Find(seed.CamryId).Status);
            }
        }

        // ---------- Consistency ----------

        // Унікальний індекс IX_Car_VinCode: дублікат VIN відхиляє сама БД, навіть без перевірки в коді
        [Fact]
        public void Consistency_duplicate_vin_is_rejected_by_database()
        {
            using (var db = new ImportContext())
            {
                db.Cars.Add(NewCar("1HGCM82633A004352")); // VIN уже є в seed (Camry)
                var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
                Assert.Contains("IX_Car_VinCode", ex.GetBaseException().Message);
            }
        }

        [Fact]
        public void Consistency_duplicate_email_is_rejected_by_database()
        {
            using (var db = new ImportContext())
            {
                db.Clients.Add(new Client { Name = "Дублікат", Email = "client1@test.com", PasswordHash = "x" });
                var ex = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
                Assert.Contains("IX_User_Email", ex.GetBaseException().Message);
            }
        }

        // Зовнішній ключ: заявка на неіснуючого клієнта не зберігається
        [Fact]
        public void Consistency_foreign_key_violation_is_rejected()
        {
            using (var db = new ImportContext())
            {
                db.Orders.Add(new Order { ClientId = -1, MaxBudget = 1000, CreatedAt = DateTime.Now });
                Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            }
        }

        // Валідація моделі ([Required], [Range], [StringLength]) перевіряється до запису в БД
        [Fact]
        public void Consistency_invalid_entity_is_rejected_by_validation()
        {
            using (var db = new ImportContext())
            {
                db.Cars.Add(new Car { VinCode = "SHORT", Make = null, Model = "X", Year = 1800, AuctionPrice = 0 });
                var ex = Assert.Throws<DbEntityValidationException>(() => db.SaveChanges());
                var errors = ex.EntityValidationErrors.SelectMany(e => e.ValidationErrors).Select(e => e.PropertyName).ToList();
                Assert.Contains("VinCode", errors);
                Assert.Contains("Make", errors);
                Assert.Contains("Year", errors);
                Assert.Contains("AuctionPrice", errors);
            }
        }

        // Форма додавання авто з наявним VIN: помилка біля поля, другий запис не створюється
        [Fact]
        public void Consistency_cars_create_reports_duplicate_vin()
        {
            using (var controller = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
            {
                var result = controller.Create(NewCar("2T1BURHE0JC034461"));
                Assert.IsType<ViewResult>(result);
                Assert.False(controller.ModelState.IsValidField("VinCode"));
            }
            using (var db = new ImportContext())
                Assert.Equal(1, db.Cars.Count(c => c.VinCode == "2T1BURHE0JC034461"));
        }

        // ---------- Isolation ----------

        // Втрачене оновлення: два користувачі прочитали авто, перший зберіг — збереження другого
        // відхиляється через RowVersion, і в БД лишаються дані першого
        [Fact]
        public void Isolation_concurrent_update_raises_concurrency_exception()
        {
            using (var first = new ImportContext())
            using (var second = new ImportContext())
            {
                var carA = first.Cars.Find(seed.CamryId);
                var carB = second.Cars.Find(seed.CamryId);

                carA.UpdatePrice(13000);
                first.SaveChanges();

                carB.UpdatePrice(11000);
                Assert.Throws<DbUpdateConcurrencyException>(() => second.SaveChanges());
            }
            using (var db = new ImportContext())
                Assert.Equal(13000, db.Cars.Find(seed.CamryId).AuctionPrice);
        }

        // Те саме через інтерфейс: менеджер зберігає форму, відкриту до чужої зміни
        [Fact]
        public void Isolation_stale_edit_form_is_rejected()
        {
            Car form;
            using (var db = new ImportContext())
                form = db.Cars.AsNoTracking().Single(c => c.CarId == seed.CamryId); // форма відкрита

            using (var db = new ImportContext())
            {
                db.Cars.Find(seed.CamryId).UpdatePrice(15000); // інший менеджер встиг зберегти
                db.SaveChanges();
            }

            form.AuctionPrice = 9000;
            using (var controller = Web.Controller<CarsController>(seed.ManagerId, UserRole.Manager))
            {
                var result = controller.Edit(form);
                Assert.IsType<ViewResult>(result);
                Assert.False(controller.ModelState.IsValid);
            }
            using (var db = new ImportContext())
                Assert.Equal(15000, db.Cars.Find(seed.CamryId).AuctionPrice);
        }

        // Два клієнти одночасно замовляють одне авто: обидва пройшли перевірку «авто вільне»,
        // але авто захоплюється оптимістичним блокуванням (як OrdersController.ClaimCar),
        // тож друга заявка не зберігається — разом з нею відкочується весь SaveChanges
        [Fact]
        public void Isolation_two_clients_cannot_order_the_same_car()
        {
            using (var first = new ImportContext())
            using (var second = new ImportContext())
            {
                var carA = first.Cars.Find(seed.CivicId);
                var carB = second.Cars.Find(seed.CivicId);

                var orderA = first.Clients.Find(seed.Client1Id).CreateOrder(20000);
                orderA.AssignCar(carA);
                first.Entry(carA).State = EntityState.Modified;
                first.Orders.Add(orderA);

                var orderB = second.Clients.Find(seed.Client2Id).CreateOrder(20000);
                orderB.AssignCar(carB);
                second.Entry(carB).State = EntityState.Modified;
                second.Orders.Add(orderB);

                first.SaveChanges();
                Assert.Throws<DbUpdateConcurrencyException>(() => second.SaveChanges());
            }
            using (var db = new ImportContext())
            {
                Assert.Equal(1, db.Orders.Count(o => o.CarId == seed.CivicId));
                Assert.Equal(seed.Client1Id, db.Orders.Single(o => o.CarId == seed.CivicId).ClientId);
            }
        }

        // Рівень ізоляції READ COMMITTED (типовий у застосунку) не допускає «брудного читання»:
        // незафіксований запис іншої транзакції не видно. EF6 створює базу з READ_COMMITTED_SNAPSHOT ON,
        // тому читач не чекає на блокування, а бачить останню зафіксовану версію рядків.
        // Для порівняння: на READ UNCOMMITTED той самий незафіксований запис видно.
        [Fact]
        public void Isolation_read_committed_prevents_dirty_reads()
        {
            using (var db = new ImportContext())
            {
                bool rcsi = db.Database.SqlQuery<bool>(
                    "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()").Single();
                Assert.True(rcsi);
            }

            const string vin = "CCCCCCCCCCC000001";
            using (var writer = new ImportContext())
            using (var tx = writer.Database.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                writer.Cars.Add(NewCar(vin));
                writer.SaveChanges(); // записано, але не зафіксовано

                Assert.Equal(1, CountCars(vin, IsolationLevel.ReadUncommitted)); // брудне читання
                Assert.Equal(0, CountCars(vin, IsolationLevel.ReadCommitted));   // лише зафіксовані дані

                tx.Rollback();
            }
            Assert.Equal(0, CountCars(vin, IsolationLevel.ReadUncommitted));      // після відкату запису немає
        }

        private static int CountCars(string vin, IsolationLevel level)
        {
            using (var db = new ImportContext())
            {
                var connection = (SqlConnection)db.Database.Connection;
                connection.Open();
                using (var tx = connection.BeginTransaction(level))
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = "SET LOCK_TIMEOUT 5000; SELECT COUNT(*) FROM dbo.Cars WHERE VinCode = @vin";
                    command.Parameters.AddWithValue("@vin", vin);
                    int count = (int)command.ExecuteScalar();
                    tx.Commit();
                    return count;
                }
            }
        }

        // ---------- Durability ----------

        // Після Commit дані лежать у файлах бази: їх видно через новий контекст і нове фізичне
        // з'єднання (пул з'єднань очищено). Це перевіряє видимість після фіксації; стійкість до
        // збою живлення забезпечує журнал транзакцій SQL Server і тестом не моделюється.
        [Fact]
        public void Durability_committed_data_is_visible_from_new_connection()
        {
            int carId;
            using (var db = new ImportContext())
            {
                var car = NewCar("DDDDDDDDDDD000001");
                db.Cars.Add(car);
                db.SaveChanges();
                carId = car.CarId;
            }

            SqlConnection.ClearAllPools();

            using (var db = new ImportContext())
            {
                var car = db.Cars.Find(carId);
                Assert.NotNull(car);
                Assert.Equal("DDDDDDDDDDD000001", car.VinCode);
            }
        }
    }
}
