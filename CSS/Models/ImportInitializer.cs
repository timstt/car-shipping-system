using System.Data.Entity;

namespace ASTCUSA.Models
{
    // Підключається в Global.asax: Database.SetInitializer(new ImportInitializer())
    public class ImportInitializer : DropCreateDatabaseIfModelChanges<ImportContext>
    {
        protected override void Seed(ImportContext db)
        {
            // Тестові користувачі: у БД зберігається лише PBKDF2-хеш пароля
            db.Clients.Add(WithPassword(new Client { Name = "Іван Петренко", Email = "ivan@mail.com", PhoneNumber = "+380501112233", Address = "Чернігів" }, "Ivan#2026"));
            db.Clients.Add(WithPassword(new Client { Name = "Марія Шевчук", Email = "maria@mail.com", PhoneNumber = "+380671234567", Address = "Київ" }, "Maria#2026"));
            db.Managers.Add(WithPassword(new Manager { Name = "Олена Коваль", Email = "manager@mail.com" }, "Manager#2026"));
            db.Logisticians.Add(WithPassword(new Logistician { Name = "Андрій Мельник", Email = "logist@mail.com" }, "Logist#2026"));

            db.Cars.Add(new Car { VinCode = "1HGCM82633A004352", Make = "Toyota", Model = "Camry", Year = 2019, AuctionPrice = 12000, Status = CarStatus.Available });
            db.Cars.Add(new Car { VinCode = "2T1BURHE0JC034461", Make = "Honda", Model = "Civic", Year = 2020, AuctionPrice = 14500, Status = CarStatus.Available });
            db.Cars.Add(new Car { VinCode = "1FA6P8TH5K5123456", Make = "Ford", Model = "Mustang", Year = 2018, AuctionPrice = 17800, Status = CarStatus.Available });
            db.Cars.Add(new Car { VinCode = "5YJ3E1EA7KF317000", Make = "Tesla", Model = "Model 3", Year = 2021, AuctionPrice = 24500, Status = CarStatus.Available });
            db.Cars.Add(new Car { VinCode = "WBA8E9C51GK644321", Make = "BMW", Model = "328i", Year = 2016, AuctionPrice = 9900, Status = CarStatus.Available });
            db.Cars.Add(new Car { VinCode = "JTDKARFU0K3087654", Make = "Toyota", Model = "Prius", Year = 2019, AuctionPrice = 11200, Status = CarStatus.Available });

            db.SaveChanges();
        }

        private static T WithPassword<T>(T user, string password) where T : User
        {
            user.SetPassword(password);
            return user;
        }
    }
}
