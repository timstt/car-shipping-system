using System.Data.Entity;

namespace ASTCUSA.Models.Analytics
{
    public class AnalyticsContext : DbContext
    {
        // Ім'я рядка підключення з Web.config (<add name="AnalyticsConnection" .../>)
        public AnalyticsContext() : base("AnalyticsConnection") { }

        public DbSet<AnalyticsUser> Users { get; set; }
        public DbSet<AnalyticsCar> Cars { get; set; }
        public DbSet<AnalyticsOrder> Orders { get; set; }
        public DbSet<AnalyticsInsurance> Insurances { get; set; }
        public DbSet<AnalyticsInvoice> Invoices { get; set; }
        public DbSet<AnalyticsDelivery> Deliveries { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Грошові стовпці в базі — DECIMAL(12, 2)
            modelBuilder.Properties<decimal>().Configure(p => p.HasPrecision(12, 2));
            // Дати в базі — DATETIME2
            modelBuilder.Properties<System.DateTime>().Configure(p => p.HasColumnType("datetime2"));
        }
    }
}
