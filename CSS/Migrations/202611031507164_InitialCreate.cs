namespace ASTCUSA.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Cars",
                c => new
                    {
                        CarId = c.Int(nullable: false, identity: true),
                        VinCode = c.String(nullable: false, maxLength: 17),
                        Make = c.String(nullable: false),
                        Model = c.String(nullable: false),
                        Year = c.Int(nullable: false),
                        AuctionPrice = c.Double(nullable: false),
                        Status = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.CarId);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                        Email = c.String(nullable: false, maxLength: 256),
                        PasswordHash = c.String(nullable: false, maxLength: 256),
                        PhoneNumber = c.String(),
                        Address = c.String(),
                        Notification = c.String(),
                        Discriminator = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Email, unique: true, name: "IX_User_Email");
            
            CreateTable(
                "dbo.Orders",
                c => new
                    {
                        OrderId = c.Int(nullable: false, identity: true),
                        MaxBudget = c.Double(nullable: false),
                        FinalPrice = c.Double(),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        ClientId = c.Int(nullable: false),
                        ManagerId = c.Int(),
                        CarId = c.Int(),
                        IsConfirmedByClient = c.Boolean(nullable: false),
                        Review = c.String(),
                    })
                .PrimaryKey(t => t.OrderId)
                .ForeignKey("dbo.Cars", t => t.CarId)
                .ForeignKey("dbo.Users", t => t.ClientId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.ManagerId)
                .Index(t => t.ClientId)
                .Index(t => t.ManagerId)
                .Index(t => t.CarId);
            
            CreateTable(
                "dbo.Deliveries",
                c => new
                    {
                        OrderId = c.Int(nullable: false),
                        TrackingNumber = c.String(),
                        CurrentLocation = c.String(),
                        IsCustomsCleared = c.Boolean(nullable: false),
                        Status = c.String(),
                        PhotoReportNote = c.String(),
                        HasCargoInsurance = c.Boolean(nullable: false),
                        CargoInsuranceCost = c.Double(nullable: false),
                        MandateGeneratedAt = c.DateTime(),
                        LogisticianId = c.Int(),
                    })
                .PrimaryKey(t => t.OrderId)
                .ForeignKey("dbo.Users", t => t.LogisticianId)
                .ForeignKey("dbo.Orders", t => t.OrderId)
                .Index(t => t.OrderId)
                .Index(t => t.LogisticianId);
            
            CreateTable(
                "dbo.Insurances",
                c => new
                    {
                        InsuranceId = c.Int(nullable: false, identity: true),
                        Type = c.String(nullable: false),
                        Price = c.Double(nullable: false),
                        OrderId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.InsuranceId)
                .ForeignKey("dbo.Orders", t => t.OrderId, cascadeDelete: true)
                .Index(t => t.OrderId);
            
            CreateTable(
                "dbo.Invoices",
                c => new
                    {
                        InvoiceId = c.Int(nullable: false, identity: true),
                        OrderId = c.Int(nullable: false),
                        TotalAmount = c.Double(nullable: false),
                        IsPaid = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.InvoiceId)
                .ForeignKey("dbo.Orders", t => t.OrderId, cascadeDelete: true)
                .Index(t => t.OrderId, unique: true, name: "IX_Invoice_OrderId");
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Orders", "ManagerId", "dbo.Users");
            DropForeignKey("dbo.Invoices", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Insurances", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Deliveries", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Deliveries", "LogisticianId", "dbo.Users");
            DropForeignKey("dbo.Orders", "ClientId", "dbo.Users");
            DropForeignKey("dbo.Orders", "CarId", "dbo.Cars");
            DropIndex("dbo.Invoices", "IX_Invoice_OrderId");
            DropIndex("dbo.Insurances", new[] { "OrderId" });
            DropIndex("dbo.Deliveries", new[] { "LogisticianId" });
            DropIndex("dbo.Deliveries", new[] { "OrderId" });
            DropIndex("dbo.Orders", new[] { "CarId" });
            DropIndex("dbo.Orders", new[] { "ManagerId" });
            DropIndex("dbo.Orders", new[] { "ClientId" });
            DropIndex("dbo.Users", "IX_User_Email");
            DropTable("dbo.Invoices");
            DropTable("dbo.Insurances");
            DropTable("dbo.Deliveries");
            DropTable("dbo.Orders");
            DropTable("dbo.Users");
            DropTable("dbo.Cars");
        }
    }
}
