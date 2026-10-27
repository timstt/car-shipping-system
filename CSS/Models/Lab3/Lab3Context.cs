using System.Data.Entity;

namespace ASTCUSA.Models.Lab3
{
    // Контекст окремої бази astcusa_lab3 (лабораторна №3).
    // Базу створює SQL-скрипт docs/lab3/create_and_seed.sql (див. Lab3Database),
    // тому в Global.asax для цього контексту вимкнено ініціалізатор:
    // Database.SetInitializer<Lab3Context>(null) — EF не створює й не змінює цю базу.
    public class Lab3Context : DbContext
    {
        // Ім'я рядка підключення з Web.config (<add name="Lab3Connection" .../>)
        public Lab3Context() : base("Lab3Connection") { }

        public DbSet<Lab3User> Users { get; set; }
        public DbSet<Lab3Car> Cars { get; set; }
        public DbSet<Lab3Order> Orders { get; set; }
        public DbSet<Lab3Insurance> Insurances { get; set; }
        public DbSet<Lab3Invoice> Invoices { get; set; }
        public DbSet<Lab3Delivery> Deliveries { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Грошові стовпці в базі — DECIMAL(12, 2)
            modelBuilder.Properties<decimal>().Configure(p => p.HasPrecision(12, 2));
            // Дати в базі — DATETIME2
            modelBuilder.Properties<System.DateTime>().Configure(p => p.HasColumnType("datetime2"));
        }
    }
}
