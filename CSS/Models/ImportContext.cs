using System.Data.Entity;

namespace ASTCUSA.Models
{
    public class ImportContext : DbContext
    {
        // Ім'я рядка підключення з Web.config (<connectionStrings><add name="DbConnection" .../>)
        public ImportContext() : base("DbConnection") { }

        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Manager> Managers { get; set; }
        public DbSet<Logistician> Logisticians { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Insurance> Insurances { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
    }
}
