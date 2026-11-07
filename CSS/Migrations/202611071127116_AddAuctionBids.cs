namespace ASTCUSA.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    // Ставки менеджера на зовнішніх аукціонах (лот, посилання, сума, результат торгів).
    // Лише додає нову таблицю — наявні дані не змінюються.
    public partial class AddAuctionBids : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AuctionBids",
                c => new
                    {
                        AuctionBidId = c.Int(nullable: false, identity: true),
                        OrderId = c.Int(nullable: false),
                        Auction = c.String(nullable: false, maxLength: 50),
                        LotNumber = c.String(nullable: false, maxLength: 30),
                        LotUrl = c.String(nullable: false, maxLength: 500),
                        Amount = c.Double(nullable: false),
                        Result = c.Int(nullable: false),
                        PlacedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.AuctionBidId)
                .ForeignKey("dbo.Orders", t => t.OrderId, cascadeDelete: true)
                .Index(t => t.OrderId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AuctionBids", "OrderId", "dbo.Orders");
            DropIndex("dbo.AuctionBids", new[] { "OrderId" });
            DropTable("dbo.AuctionBids");
        }
    }
}
