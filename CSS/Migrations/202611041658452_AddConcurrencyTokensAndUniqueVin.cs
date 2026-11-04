namespace ASTCUSA.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    // ЛР5 (ACID): маркери паралельного доступу RowVersion для авто й заявок (Isolation)
    // та унікальний VIN на рівні БД (Consistency). Лише додає стовпці й індекс — дані не видаляються.
    public partial class AddConcurrencyTokensAndUniqueVin : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Cars", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
            AddColumn("dbo.Orders", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
            CreateIndex("dbo.Cars", "VinCode", unique: true, name: "IX_Car_VinCode");
        }
        
        public override void Down()
        {
            DropIndex("dbo.Cars", "IX_Car_VinCode");
            DropColumn("dbo.Orders", "RowVersion");
            DropColumn("dbo.Cars", "RowVersion");
        }
    }
}
