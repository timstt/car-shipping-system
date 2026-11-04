using System;
using System.Data.Entity;
using ASTCUSA.Models;

namespace ASTCUSA.Tests.Infrastructure
{
    // Ідентифікатори тестових даних, створених TestDb.Reset()
    public class Seed
    {
        public int Client1Id, Client2Id, ManagerId, LogisticianId;
        public int CamryId, CivicId;
    }

    // Окрема тестова база astcusa_tests (рядок підключення DbConnection в App.config).
    // Схема створюється тими самими міграціями, що й робоча база, тож тести перевіряють і міграції.
    public static class TestDb
    {
        private static readonly object Sync = new object();
        private static bool migrated;
        private static string passwordHash;

        public static void EnsureMigrated()
        {
            lock (Sync)
            {
                if (migrated) return;
                using (var db = new ImportContext())
                {
                    // Захист: тести очищають таблиці, тож працюють лише з базою *_tests
                    string name = db.Database.Connection.Database;
                    if (!name.EndsWith("_tests", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Тести можна запускати лише на базі *_tests, а не на " + name);
                }
                Database.SetInitializer(new ImportInitializer());
                using (var db = new ImportContext())
                {
                    db.Database.Initialize(true);
                }
                // PBKDF2 зі 100 000 ітерацій повільний — хеш обчислюється один раз для всіх тестів
                passwordHash = PasswordHasher.Hash("Test#2026");
                migrated = true;
            }
        }

        // Очистити всі таблиці й заповнити мінімальними даними: 2 клієнти, менеджер, логіст, 2 авто
        public static Seed Reset()
        {
            EnsureMigrated();
            using (var db = new ImportContext())
            {
                db.Database.ExecuteSqlCommand(
                    "DELETE FROM dbo.Insurances; DELETE FROM dbo.Invoices; DELETE FROM dbo.Deliveries; " +
                    "DELETE FROM dbo.Orders; DELETE FROM dbo.Users; DELETE FROM dbo.Cars;");

                var c1 = new Client { Name = "Клієнт Один", Email = "client1@test.com", PasswordHash = passwordHash, PhoneNumber = "+380500000001" };
                var c2 = new Client { Name = "Клієнт Два", Email = "client2@test.com", PasswordHash = passwordHash };
                var m = new Manager { Name = "Менеджер", Email = "manager@test.com", PasswordHash = passwordHash };
                var l = new Logistician { Name = "Логіст", Email = "logist@test.com", PasswordHash = passwordHash };
                var camry = new Car { VinCode = "1HGCM82633A004352", Make = "Toyota", Model = "Camry", Year = 2019, AuctionPrice = 12000, Status = CarStatus.Available };
                var civic = new Car { VinCode = "2T1BURHE0JC034461", Make = "Honda", Model = "Civic", Year = 2020, AuctionPrice = 14500, Status = CarStatus.Available };
                db.Users.Add(c1); db.Users.Add(c2); db.Users.Add(m); db.Users.Add(l);
                db.Cars.Add(camry); db.Cars.Add(civic);
                db.SaveChanges();

                return new Seed
                {
                    Client1Id = c1.Id, Client2Id = c2.Id, ManagerId = m.Id, LogisticianId = l.Id,
                    CamryId = camry.CarId, CivicId = civic.CarId
                };
            }
        }

        // Додати заявку напряму в БД (для тестів, яким потрібна заявка в певному статусі)
        public static int AddOrder(int clientId, int? carId, OrderStatus status, double budget = 15000)
        {
            using (var db = new ImportContext())
            {
                var order = new Order { ClientId = clientId, CarId = carId, Status = status, MaxBudget = budget, CreatedAt = DateTime.Now };
                db.Orders.Add(order);
                db.SaveChanges();
                return order.OrderId;
            }
        }
    }
}
